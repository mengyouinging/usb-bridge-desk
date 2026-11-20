# R3 1.1.0 Windows test - run 3

- build: success
- silent install: success
- desktop capture: failure
- installer: SIZE=44312634 | SHA256=3A07A99C7F0B1E2D026FC1C494535ACC0F957ED8478304ED9611755004677AD5 | INSTALL_EXIT=3010
- driver exit: DRIVER_EXIT=1
- alive 30s: False
- clean exit: False (forced: False, code: -1073741189)
- screenshots: 0
- failures: App exited (-1073741189) while waiting for native main window

