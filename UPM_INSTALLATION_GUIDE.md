# Mirror UPM Package - Installation Guide

This document explains how to use Mirror as a Unity Package Manager (UPM) package.

## What Was Changed

Your Mirror project has been converted into a proper UPM package with the following additions:

1. **package.json** - Package manifest with metadata
2. **README.md** - Package documentation
3. **CHANGELOG.md** - Version history
4. **Meta files** - Unity metadata for the new files

## Installation Methods

### Method 1: Install from Git URL (Recommended for Users)

1. Open Unity Package Manager (Window > Package Manager)
2. Click the **'+'** button in the top-left corner
3. Select **"Add package from git URL..."**
4. Enter one of the following:
   
   **From GitHub (if you push to GitHub):**
   ```
   https://github.com/MirrorNetworking/Mirror.git?path=/Assets/Mirror
   ```
   
   **From local repository:**
   ```
   file:.../Mirror/Assets/Mirror
   ```

### Method 2: Install via manifest.json

1. Navigate to your Unity project's `Packages` folder
2. Open `manifest.json` in a text editor
3. Add the following to the `dependencies` section:

   ```json
   {
     "dependencies": {
       "com.mirror-networking.mirror": "https://github.com/MirrorNetworking/Mirror.git?path=/Assets/Mirror"
     }
   }
   ```
   
   Or for local testing:
   ```json
   {
     "dependencies": {
       "com.mirror-networking.mirror": "file:.../Mirror/Assets/Mirror"
     }
   }
   ```

4. Save the file and return to Unity - it will automatically install the package

### Method 3: Local Package (For Development)

1. In your Unity project, open Package Manager
2. Click **'+'** > **"Add package from disk..."**
3. Navigate to `...\Mirror\Assets\Mirror`
4. Select `package.json`

## Publishing Your Package

### Option 1: GitHub Repository

1. Create a new GitHub repository or use your existing one
2. Push your code:
   ```powershell
   cd "...\Mirror"
   git add .
   git commit -m "Convert to UPM package"
   git push
   ```

3. Users can then install via:
   ```
   https://github.com/MirrorNetworking/Mirror.git?path=/Assets/Mirror
   ```

### Option 2: Git Tag/Release (Recommended)

For versioned releases:

1. Create a git tag:
   ```powershell
   git tag v89.7.2
   git push origin v89.7.2
   ```

2. Users can install a specific version:
   ```
   https://github.com/MirrorNetworking/Mirror.git?path=/Assets/Mirror#v89.7.2
   ```

### Option 3: OpenUPM or npm Registry

For advanced users, you can publish to:
- **OpenUPM**: https://openupm.com/
- **npm Registry**: For private or public npm packages
- **Unity Package Manager Registry**: For enterprise solutions

## Package Structure

```
Assets/Mirror/
├── package.json          # Package manifest (required)
├── README.md            # Package documentation
├── CHANGELOG.md         # Version history
├── Authenticators/      # Mirror authenticators
├── Components/          # Mirror components
├── Core/               # Core networking code
├── Editor/             # Editor scripts
├── Examples/           # Example scenes (optional: rename to Examples~)
├── Hosting/            # Hosting utilities
├── Plugins/            # Plugin files
├── Tests/              # Unit tests
└── Transports/         # Network transports
```

## Updating the Package Version

When you make changes and want to release a new version:

1. Update the version in `package.json`:
   ```json
   "version": "89.7.3",
   ```

2. Update `CHANGELOG.md` with your changes

3. Commit and push:
   ```powershell
   git add .
   git commit -m "Release v89.7.3"
   git tag v89.7.3
   git push origin master --tags
   ```

## Optional: Exclude Examples from Package

If you want examples to be available as samples (not included by default):

1. Rename `Examples` folder to `Examples~` (the tilde excludes it from the package)
2. Update the samples in `package.json` (already configured)

Users can then import examples via Package Manager > Mirror > Samples > Import

## Testing Your Package Locally

Before publishing, test the package:

1. Create a new Unity project
2. Add your package using the local file method
3. Verify all features work correctly
4. Check for any missing dependencies

## Troubleshooting

### Package doesn't appear in Package Manager
- Ensure `package.json` is valid JSON
- Check that the `name` field follows the format: `com.company.package`
- Verify the `version` field is a valid semantic version (e.g., "1.0.0")

### Missing dependencies
- Add required packages to the `dependencies` field in `package.json`

### Assembly definition issues
- Ensure all `.asmdef` files are properly configured
- Check that assembly references are correct

## Additional Resources

- **Unity Package Manager Documentation**: https://docs.unity3d.com/Manual/Packages.html
- **Creating Custom Packages**: https://docs.unity3d.com/Manual/CustomPackages.html
- **Git Dependencies**: https://docs.unity3d.com/Manual/upm-git.html

## Support

For Mirror-specific questions:
- Discord: https://discord.gg/xVW4nU4C34
- GitHub: https://github.com/MirrorNetworking/Mirror
- Documentation: https://mirror-networking.gitbook.io/
