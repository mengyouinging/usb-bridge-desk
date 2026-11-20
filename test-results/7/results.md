# R3 1.1.0 Windows test - run 7

- build: success
- silent install: success
- installed backend: success
- desktop capture: failure
- installer: SIZE=45277484 | SHA256=D8D2543D48A2A300C2716C2BF75559BD1AE36C25C5426F0B4C901E2824F6207B | INSTALL_EXIT=3010
- driver exit: DRIVER_EXIT=1
- alive 30s: True
- clean exit: False (forced: True, code: -1)
- screenshots: 6
- failures: Timed out waiting for notification icon after pointer click. 

- service_start passed=True {"State":"Running","ProcessId":8180,"PathName":"\"C:\\Program Files\\USB Bridge Desk\\resources\\host\\usbipd.exe\" server"}
- tcp_3240_listener passed=True [{"LocalAddress":"::","LocalPort":3240,"OwningProcess":8180}]
- host_device_enumeration passed=True {"exitCode":0,"devices":0}
- usbip_devlist_handshake passed=True {"endpoint":"127.0.0.1:3240","version":"0x0111","request":"OP_REQ_DEVLIST","reply":"OP_REP_DEVLIST","status":0,"exports":0}
- installed_client_loopback_list passed=True {"exitCode":0,"exports":0,"endpoint":"127.0.0.1:3240"}
- client_virtual_ports passed=True {"exitCode":0,"importedPorts":0}
- restore_service passed=True {"restored":"Stopped"}
- 01-overview.png (overview) sha256=5161A11DFAAFD0A78EC2BBAF810E7DF1746BCFA8AE0354989767E5689A137E0F
- 02-local.png (host) sha256=5CAAC65BEA1F49A9C8C446A5ACE1B911566D9DE45C02B8595AF1B84FEDF5D40A
- 03-remote.png (client) sha256=566C69BBF8DD48DDF0B14221C33D8D114348006241FEBCC10F57E3DB71513BAE
- 04-mappings.png (mappings) sha256=F62E74443DCD273C91256D2F0B9F45767DD90A86F8F50EC9331FE7469FFA0A0A
- 05-dark-acrylic.png (settings dark / acrylic / desktop blend unverified) sha256=9F48A8A020B36E9A2CA0F586A597B4D0B1B842F0A720CA9C43D00B10F964CC32
- 06-dark-overview.png (overview dark / acrylic / desktop blend unverified) sha256=7259149D901D327883BA13CEA29BD2C7D4AF3A783B4933E2A3B0AED711B02A53
