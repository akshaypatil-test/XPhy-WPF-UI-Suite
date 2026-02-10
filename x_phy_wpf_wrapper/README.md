# X-PHY WPF Wrapper

This is a C++/CLI wrapper project that provides a managed interface to the `ApplicationController` class from `detection_program_lib.lib`.

## Overview

The wrapper exposes the following functionality to .NET/WPF applications:

1. **Open Results Folder** - Opens the Windows Explorer to the results directory
2. **Start Web Surfing Video Detection** - Starts the deepfake detection in Web Surfing mode
3. **Stop Video Detection** - Stops an ongoing detection session
4. **Get Results Directory** - Returns the path to the results directory

## Usage

The wrapper is used by the `x_phy_wpf_ui` WPF project. The wrapper DLL must be built before the WPF project can reference it.

## Dependencies

- `detection_program_lib.lib` - Contains the backend detection logic
- OpenCV (via vcpkg)
- TensorFlow (via vcpkg)
- Other dependencies as specified in the main solution

## Building

The project is configured to build as a C++/CLI dynamic library (DLL) that can be consumed by .NET applications.

## Notes

- The callback for detection results is invoked from a background thread. The WPF application should use `Dispatcher.Invoke` if UI updates are needed.
- The wrapper handles proper cleanup of native resources in the finalizer.
