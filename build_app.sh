#!/bin/bash
# Exit on any error
set -e

echo "=== Cleaning previous app bundle ==="
rm -rf PokeSaveEditor.app

echo "=== Building PokeSaveEditor ==="
/usr/local/share/dotnet/dotnet build -c Debug src/PokeSaveEditor.UI/PokeSaveEditor.UI.csproj

echo "=== Creating directory structure ==="
mkdir -p PokeSaveEditor.app/Contents/MacOS
mkdir -p PokeSaveEditor.app/Contents/Resources

echo "=== Copying binaries ==="
cp -R src/PokeSaveEditor.UI/bin/Debug/net9.0/. PokeSaveEditor.app/Contents/MacOS/

echo "=== Generating macOS icon (.icns) ==="
ICON_SRC="/Users/bekirkarakose/.gemini/antigravity-ide/brain/3cec234b-fba0-4ce4-aeee-ac4477d2b68c/masterball_icon_1784149202082.png"
if [ -f "$ICON_SRC" ]; then
    rm -rf AppIcon.iconset
    mkdir -p AppIcon.iconset
    sips -s format png -z 16 16 "$ICON_SRC" --out AppIcon.iconset/icon_16x16.png
    sips -s format png -z 32 32 "$ICON_SRC" --out AppIcon.iconset/icon_16x16_2x.png && mv AppIcon.iconset/icon_16x16_2x.png AppIcon.iconset/icon_16x16@2x.png
    sips -s format png -z 32 32 "$ICON_SRC" --out AppIcon.iconset/icon_32x32.png
    sips -s format png -z 64 64 "$ICON_SRC" --out AppIcon.iconset/icon_32x32_2x.png && mv AppIcon.iconset/icon_32x32_2x.png AppIcon.iconset/icon_32x32@2x.png
    sips -s format png -z 128 128 "$ICON_SRC" --out AppIcon.iconset/icon_128x128.png
    sips -s format png -z 256 256 "$ICON_SRC" --out AppIcon.iconset/icon_128x128_2x.png && mv AppIcon.iconset/icon_128x128_2x.png AppIcon.iconset/icon_128x128@2x.png
    sips -s format png -z 256 256 "$ICON_SRC" --out AppIcon.iconset/icon_256x256.png
    sips -s format png -z 512 512 "$ICON_SRC" --out AppIcon.iconset/icon_256x256_2x.png && mv AppIcon.iconset/icon_256x256_2x.png AppIcon.iconset/icon_256x256@2x.png
    sips -s format png -z 512 512 "$ICON_SRC" --out AppIcon.iconset/icon_512x512.png
    sips -s format png -z 1024 1024 "$ICON_SRC" --out AppIcon.iconset/icon_512x512_2x.png && mv AppIcon.iconset/icon_512x512_2x.png AppIcon.iconset/icon_512x512@2x.png
    iconutil -c icns AppIcon.iconset
    mv AppIcon.icns PokeSaveEditor.app/Contents/Resources/AppIcon.icns
    rm -rf AppIcon.iconset
else
    echo "Warning: Master Ball PNG icon not found. Skipping icon generation."
fi

echo "=== Creating Info.plist ==="
cat << 'EOF' > PokeSaveEditor.app/Contents/Info.plist
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>English</string>
    <key>CFBundleExecutable</key>
    <string>PokeSaveEditor.UI</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon</string>
    <key>CFBundleIdentifier</key>
    <string>com.pokesaveeditor.app</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleName</key>
    <string>PokeSaveEditor</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0.0</string>
    <key>CFBundleSignature</key>
    <string>????</string>
    <key>CFBundleVersion</key>
    <string>1.0.0</string>
    <key>LSMinimumSystemVersion</key>
    <string>10.12</string>
    <key>NSPrincipalClass</key>
    <string>NSApplication</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
EOF

echo "=== Setting executable permissions ==="
chmod +x PokeSaveEditor.app/Contents/MacOS/PokeSaveEditor.UI

echo "=== Removing quarantine flags ==="
xattr -cr PokeSaveEditor.app

echo "=== Ad-hoc signing bundle (Required for Apple Silicon) ==="
codesign --force --deep --sign - PokeSaveEditor.app

echo "=== Build and packaging complete ==="
echo "You can now run the app via: open PokeSaveEditor.app"
