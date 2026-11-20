# R3 1.1.0 Windows test - run 4

- build: success
- silent install: success
- desktop capture: failure
- installer: SIZE=44958698 | SHA256=E041541064E5FD7D863CDB5364D11A0586525F4731F1BC6362C81383999A5289 | INSTALL_EXIT=3010
- driver exit: DRIVER_EXIT=1
- alive 30s: True
- clean exit: False (forced: True, code: -1)
- screenshots: 6
- failures: Exception calling "GetClickablePoint" with "0" argument(s): "The AutomationElement has no clickable point."

- 01-overview.png (overview) sha256=948905F3E12C06FEFFD821561D8E7A04FD6E391685BA6B295A46E6F4C2D90142
- 02-local.png (host) sha256=7BE774FDE491C8076C97519C8E14E97DFB88F0D16BA2D2F00D5C1658FADB9CAB
- 03-remote.png (client) sha256=58A0E22658FADE94043339F0621C47DDF052D0607E60EB9D39A2315193FF20B7
- 04-mappings.png (mappings) sha256=0AE194FF93C8D9DEBFFEDCBA917DD23BA4498EAB0B3DE228DFB36FB00B6C1C81
- 05-dark-acrylic.png (settings dark / acrylic selected) sha256=5B08A9977E468BEE72C540D4596BA18E7AFDA3E9AEEE0E9DF5D895C01653AE70
- 06-dark-overview.png (overview dark / acrylic selected) sha256=C8D610A89DFDD1230B1A466BD225256D1A5D5C9E31B4B457E8624EFAB2F5603B
