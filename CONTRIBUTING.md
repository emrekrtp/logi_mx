# Contributing

Thank you for your interest in contributing to **MX Backlight Control**!

## How to Contribute

1. **Fork** this repository.
2. Create a new feature or fix branch:
   ```bash
   git checkout -b feat/your-feature-name
   ```
3. Make your changes and verify that the solution builds cleanly:
   ```powershell
   dotnet build MxBacklightControl.sln -c Release
   ```
4. Commit your changes with a clear commit message:
   ```bash
   git commit -m "feat: add your feature description"
   ```
5. Push your branch to your fork and open a **Pull Request**.

## Reporting Issues & Feature Requests

If you encounter a bug or want to request support for a specific keyboard model, please open an **Issue** and include:
- Keyboard model (e.g., MX Keys S, MX Mechanical Mini)
- Connection type (Bluetooth LE, Logi Bolt, Unifying, USB)
- Windows version and any status/error message shown in the app
