#!/bin/bash

# Detect OS
OS=$(uname)

if [ "$OS" == "Darwin" ]; then
    # MacOS
    sudo launchctl unload /Library/LaunchDaemons/Cloud5777.plist
    sudo launchctl disable system/Cloud5777
    sudo rm /Library/LaunchDaemons/Cloud5777.plist
    sudo launchctl bootstrap system /Library/LaunchDaemons

    sudo diskutil unmount /Users/$USER/Cloud

    sudo rm -r /Users/$USER/Cloud
    sudo rm -r /Volumes/.\$Sys/
    sudo rm -r /usr/local/share/cloud
    sudo rm /Users/$USER/Desktop/Cloud\ Settings\ 5777.htm

elif [ "$OS" == "Linux" ]; then
    # Linux
    sudo systemctl stop Cloud5777.service
    sudo systemctl disable Cloud5777.service
    sudo rm /etc/systemd/system/Cloud5777.service
    sudo systemctl daemon-reload

    sudo fusermount -u /home/$USER/Cloud

    sudo rm -r /home/$USER/Cloud
    sudo rm -r /var/lib/.$\Sys
    sudo rm -r /usr/share/cloud
    sudo rm /home/$USER/Desktop/Cloud\ Settings\ 5777.htm
else
    echo "Os not supported: $OS"
fi
