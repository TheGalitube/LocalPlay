# Contributing

Thanks for helping improve LocalPlay.

## Development workflow

1. Fork and clone the repository on Windows 10/11 x64.
2. Run `.\scripts\bootstrap.ps1`.
3. Create a focused branch.
4. Implement and verify the change.
5. Run `.\scripts\test.ps1`.
6. Open a pull request using the repository template.

UI and networking changes should also be tested with the native Windows app.
Changes to UxPlay must be represented as a reproducible patch under `patches/`
and applied by `scripts/bootstrap.ps1`.

## Pull requests

- Keep changes focused and explain the user-visible result.
- Do not commit `.deps`, `.tools`, `artifacts`, build output, pairing data, or
  local settings.
- Update the README when setup or user behavior changes.
- Update `THIRD_PARTY_NOTICES.md` and packaging when dependencies change.
- Never include private IP addresses, pairing records, or personal logs.

## Release check

Before tagging a release:

```powershell
.\scripts\test.ps1
.\scripts\package-portable.ps1 -Version 0.2.1
```

Extract the generated ZIP into a clean directory and verify adapter discovery,
the network test, receiver startup, audio, and at least one Apple client.
