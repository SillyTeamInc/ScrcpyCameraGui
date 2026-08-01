#!/bin/bash

set -e

APP_NAME="ScrcpyCameraGui"
TARGET_FRAMEWORK="net10.0"
RUNTIME_IDENTIFIER="linux-x64"
APP_DIR="${APP_NAME}.AppDir"
ICON_NAME=${APP_NAME,,}

echo "Starting AppImage build for $APP_NAME"

echo "Publishing dotnet project"
(cd .. && dotnet publish -c Release -r $RUNTIME_IDENTIFIER --self-contained true -p:PublishSingleFile=true)

echo "Creating appdir structure"
rm -rf "$APP_DIR"
mkdir -p "$APP_DIR/usr/bin"

cp -r ../bin/Release/$TARGET_FRAMEWORK/$RUNTIME_IDENTIFIER/publish/* "$APP_DIR/usr/bin/"

chmod +x "$APP_DIR/usr/bin/$APP_NAME"

echo "Generating .desktop file"
cat <<EOF > "$APP_DIR/$APP_NAME.desktop"
[Desktop Entry]
Type=Application
Name=$APP_NAME
Exec=$APP_NAME
Icon=$ICON_NAME
Categories=Utility;
Terminal=false
EOF

echo "Setting up application icon"

if [ -f "icon.png" ]; then
    cp icon.png "$APP_DIR/$ICON_NAME.png"
else
    echo "No 'icon.png' found in the root directory, using a placeholder svg anyway"
    cat <<EOF > "$APP_DIR/$ICON_NAME.svg"
<svg width="256" height="256" xmlns="http://www.w3.org/2000/svg">
  <rect width="100%" height="100%" fill="#4a90e2" />
  <text x="50%" y="50%" font-family="Arial" font-size="24" fill="white" alignment-baseline="middle" text-anchor="middle">$APP_NAME</text>
</svg>
EOF
fi

echo "Linking apprun"
cd "$APP_DIR"
ln -s "usr/bin/$APP_NAME" AppRun
cd ..

if [ ! -f "appimagetool-x86_64.AppImage" ]; then
    echo "Downloading appimagetool..."
    wget -q https://github.com/AppImage/AppImageKit/releases/download/continuous/appimagetool-x86_64.AppImage
    chmod +x appimagetool-x86_64.AppImage
fi

echo "Building appimage"
./appimagetool-x86_64.AppImage "$APP_DIR"

rm -rf "$APP_DIR"

echo "Build complete!!!"