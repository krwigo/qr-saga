# QR Saga

QR Saga is a minimal barcode scanner to pass the scanned URL directly to the browser resulting in increased productivity and reduced installation times.

## Installation

[QR Saga in the Microsoft Store](https://www.microsoft.com/en-us/p/qr-saga/9nc19dp5w18w).

## Problem

To install apps on a [Windows Mobile](https://en.wikipedia.org/wiki/Windows_10_Mobile) 8+, 10 device, a store URL had to be copied to the [Microsoft Edge](https://en.wikipedia.org/wiki/Microsoft_Edge#Features) browser which in turn opened the [Windows Store](https://en.wikipedia.org/wiki/Microsoft_Store) app. An installation QR code was automatically generated rather than typing the full URL each time. Scanning the QR code was accomplished by using a Chinese app from the store initially but it only showed the URL and had to be copied to the browser manually costing a lot of time and effort.

## Solution

QR Saga was created to scan the QR code and pass the resulting URL to the browser automatically. It also kept the URL in memory for those times when the store app couldn't connect and had to be re-opened again without scanning.

## Certification

Passing store certification wasn't easy at first. The store required that the app functioned as expected which meant that it had to successfully scan a barcode. Development was done using a [Lumia 950](https://en.wikipedia.org/wiki/Microsoft_Lumia_950) with a good camera, but certification was performed using lower-tier devices. Originally the submission targeted the mobile platform only and since the submission didn't yet support focusing, the certification failed.

Adding Xbox and desktop platforms to the submission helped to pass because it allowed other devices to scan with different focusing mechanisms. Imagine a store reviewer in an office somewhere in the world holding an Xbox [Kinect](https://en.wikipedia.org/wiki/Kinect) camera up to the screen or some paper to scan a barcode. Subsequent submissions were automatically passed through the system without manual certification.

## Future

Later versions were intended to pass the URL through a browser extension bypassing the entire camera system due to unreliable hardware.

There were reports of blank screens where older devices would not display any camera picture. Verbose logs were added but an investigation was never completed to trace the issue.

## Screenshots

![Screenshot 1](https://github.com/krwigo/qr-saga/blob/master/QR%20Saga/StoreAssets/Mobile-Joined-1.png)
![Screenshot 2](https://github.com/krwigo/qr-saga/blob/master/QR%20Saga/StoreAssets/Mobile-Joined-2.png)

## Behind the name

QR Saga was selected because, at the time, the store had lots of apps similar to Candy Crush *Saga*, Peggle *Deluxe*, etc.

## Compiling

Beware that only a few Windows Mobile devices were updated to the [Creators Update](https://en.wikipedia.org/wiki/Windows_10_Mobile#Creators_Update_(version_1703)_and_Fall_Creators_Update_(version_1709)) of the Windows Mobile operating system. Check your device to verify which sdk it can target. (eg; 10.0.10240.0)

A few dependencies are required.

1. Install [Visual Studio Community Edition](https://visualstudio.microsoft.com/vs/community/) *Version 2019 okay*
  * Universal Windows Platform development
  * .NET desktop development
2. Open [QR Saga.sln](https://github.com/krwigo/qr-saga/blob/master/QR%20Saga.sln)
3. Tap *Restore NuGet Packages*
4. Tap *Build Solution*
