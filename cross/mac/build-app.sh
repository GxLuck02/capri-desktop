#!/bin/sh
# Builds Capri.app for this Mac (Apple silicon) into ~/Applications, the way the Mac CI job does, ad hoc signed.
# Run from anywhere: sh cross/mac/build-app.sh
set -e
cd "$(dirname "$0")/../.."
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
version=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' cross/Capri.Cross.csproj)
mkdir -p "$work/Capri.iconset"
for s in 16 32 128 256 512; do
  sips -z $s $s cross/icon.png --out "$work/Capri.iconset/icon_${s}x${s}.png" >/dev/null
  d=$((s * 2))
  sips -z $d $d cross/icon.png --out "$work/Capri.iconset/icon_${s}x${s}@2x.png" >/dev/null
done
iconutil -c icns "$work/Capri.iconset" -o "$work/Capri.icns"
dotnet publish cross/Capri.Cross.csproj -c Release -r osx-arm64 --self-contained -o "$work/out" -p:DebugType=none
app="$work/Capri.app"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
cp -R "$work/out/." "$app/Contents/MacOS/"
sed "s/@VERSION@/$version/g" cross/mac/Info.plist > "$app/Contents/Info.plist"
cp "$work/Capri.icns" "$app/Contents/Resources/Capri.icns"
codesign --force --deep --sign - "$app"
codesign --verify "$app"
pkill -x Capri 2>/dev/null || true
mkdir -p "$HOME/Applications"
rm -rf "$HOME/Applications/Capri.app"
cp -R "$app" "$HOME/Applications/Capri.app"
echo "Capri $version is in ~/Applications. Open it from Launchpad or Spotlight."
