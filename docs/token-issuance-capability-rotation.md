# Auth-to-IAM token-issuance capability keys

## Local Development and Testing

The AppHost creates a fresh RSA key pair in memory each time it starts in `Development` or `Testing`:

- AuthService receives the private key and active key ID.
- IAMService receives only the corresponding public key, keyed by the same ID.
- Database seeders and other services receive neither capability key.
- `Staging` and `Production` do not generate or distribute local capability keys.

The key ID is the lowercase SHA-256 fingerprint of the DER-encoded public key. Restarting the AppHost therefore rotates the local key automatically without writing key material to disk.

## Rotation sequence

Production key storage and deployment are intentionally outside this repository. Operators should preserve availability and fail closed by rotating in this order:

1. Generate the replacement RSA key in the approved secret-management system.
2. Add the replacement public key to IAM's `IAM:TokenIssuanceCapability:PublicKeys` ring while retaining the current public key.
3. Deploy or reload IAM and verify that capabilities signed by both key IDs are accepted.
4. Change Auth's `Auth:TokenIssuanceCapability:ActiveKeyId` and private key to the replacement key, then deploy or reload Auth.
5. Verify new Auth capabilities carry the replacement key ID and that permission resolution succeeds.
6. Wait longer than 60 seconds, the maximum accepted capability lifetime, so every capability signed by the previous key has expired.
7. Remove the previous public key from IAM and deploy or reload IAM.
8. Verify replacement-key capabilities still succeed and previous-key or incorrectly signed capabilities are rejected.

Never place private key material in IAM configuration, application logs, source control, test output, or rotation tickets. A failed overlap, switch, or verification step must stop the rotation before the prior public key is removed.
