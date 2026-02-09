#!/usr/bin/env python3
"""
MALIEV Service Refactoring Script
Automates the standardization of Program.cs files across all microservices.
"""

import re
import sys
from pathlib import Path

def refactor_program_cs(file_path):
    """
    Refactor a Program.cs file to use standardized ServiceDefaults extensions.
    """
    print(f"Refactoring: {file_path}")

    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()

    original_content = content
    changes_made = []

    # 1. Remove Serilog using statement
    if 'using Serilog;' in content or 'using System.Threading.RateLimiting;' in content:
        content = re.sub(r'using Serilog;\s*\n', '', content)
        content = re.sub(r'using System\.Threading\.RateLimiting;\s*\n', '', content)
        changes_made.append("Removed Serilog and RateLimiting using statements")

    # 2. Replace Serilog configuration
    if 'UseSerilog' in content:
        content = re.sub(
            r'builder\.Host\.UseSerilog\([^)]+\);?\s*\n',
            '// Standard .NET logging configured via appsettings.json\n',
            content
        )
        changes_made.append("Replaced Serilog with standard logging")

    # 3. Replace AddRedisDistributedCache with AddStandardCache
    if 'AddRedisDistributedCache' in content:
        content = re.sub(
            r'builder\.AddRedisDistributedCache\(instanceName:\s*"([^"]+)"\);?',
            r'builder.AddStandardCache("\1"); // Redis + in-memory fallback, memory-optimized',
            content
        )
        changes_made.append("Replaced AddRedisDistributedCache with AddStandardCache")

    # 4. Replace AddDefaultCors with AddStandardCors
    if 'AddDefaultCors' in content:
        content = re.sub(
            r'builder\.AddDefaultCors\(\);?.*\n',
            'builder.AddStandardCors(); // CORS with fail-fast validation\n',
            content
        )
        changes_made.append("Replaced AddDefaultCors with AddStandardCors")

    # 5. Replace custom rate limiting with AddStandardRateLimiting
    # Match complex rate limiting blocks
    rate_limit_pattern = r'builder\.Services\.AddRateLimiter\(options\s*=>\s*\{[^}]+(?:\{[^}]+\}[^}]+)*\}\);?\s*\n'
    if re.search(rate_limit_pattern, content, re.DOTALL):
        content = re.sub(
            rate_limit_pattern,
            'builder.AddStandardRateLimiting(); // Memory-optimized for low-spec nodes\n',
            content,
            flags=re.DOTALL
        )
        changes_made.append("Replaced custom rate limiting with AddStandardRateLimiting")

    # 6. Remove UseSerilogRequestLogging
    if 'UseSerilogRequestLogging' in content:
        content = re.sub(r'\s*app\.UseSerilogRequestLogging\(\);?\s*\n', '', content)
        changes_made.append("Removed UseSerilogRequestLogging")

    if content != original_content:
        with open(file_path, 'w', encoding='utf-8') as f:
            f.write(content)
        print(f"  [OK] Refactored successfully")
        for change in changes_made:
            print(f"     - {change}")
        return True
    else:
        print(f"  [INFO] No changes needed")
        return False

def find_program_cs_files(base_path):
    """Find all Program.cs files in service Api projects."""
    base = Path(base_path)
    program_files = []

    # Pattern: Maliev.{Service}\Maliev.{Service}.Api\Program.cs
    for service_dir in base.iterdir():
        if service_dir.is_dir() and service_dir.name.startswith('Maliev.') and 'Service' in service_dir.name:
            api_dir = service_dir / f"{service_dir.name}.Api"
            program_cs = api_dir / "Program.cs"
            if program_cs.exists():
                program_files.append(program_cs)

    return program_files

def main():
    if len(sys.argv) > 1:
        # Specific file provided
        file_path = Path(sys.argv[1])
        if file_path.exists():
            refactor_program_cs(file_path)
        else:
            print(f"Error: File not found: {file_path}")
            sys.exit(1)
    else:
        # Refactor all services
        base_path = Path(__file__).parent.parent
        program_files = find_program_cs_files(base_path)

        print(f"Found {len(program_files)} Program.cs files to refactor\n")

        refactored_count = 0
        for program_file in sorted(program_files):
            if refactor_program_cs(program_file):
                refactored_count += 1
            print()

        print(f"\n[SUCCESS] Refactoring complete: {refactored_count}/{len(program_files)} files updated")

if __name__ == "__main__":
    main()
