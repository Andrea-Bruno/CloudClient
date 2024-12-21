#!/bin/bash

# copy files to working directory
if [ "$PWD" != /usr/share/cloud ]; then
  sudo mkdir /usr/share/cloud
  sudo chmod 777 /usr/share/cloud
  cp -R . /usr/share/cloud
  sudo chmod 777 /usr/share/cloud/install.sh
  cd /usr/share/cloud
  ./install-linux.sh
  exit
fi

if (( $EUID == 0 )); then
    echo "WARNING: Don't run as root!"
    exit
fi

# DOTNET runtime
# NOTE: You can download the runtime from here: https://dotnet.microsoft.com/en-us/download/dotnet/6.0
#       and unzip the *.tga.gz file in home/$YourUser/.dotnet

# Download the installer:
if which wget >/dev/null ; then
    sudo wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
elif which curl >/dev/null ; then
    sudo curl -L -O https://dot.net/v1/dotnet-install.sh
else
    echo "Cannot download, neither wget nor curl is available!"
    exit
fi

sudo chmod +x ./dotnet-install.sh
# info https://learn.microsoft.com/it-it/dotnet/core/tools/dotnet-install-script
./dotnet-install.sh --channel 6.0 --runtime aspnetcore

#start the application
sudo ~/.dotnet/dotnet Cloud.dll