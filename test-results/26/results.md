# R3 1.1.0 Windows test - run 26

- build: success
- silent install: success
- installed backend: success
- desktop capture: success
- installer: SIZE=45295164 | SHA256=B12B9C8BAE2500C0FD2D009E73DAC623BF4C2249B18ED5E06F159D66D85DD4E7 | INSTALL_EXIT=3010
- driver exit: DRIVER_EXIT=0
- alive 30s: True
- clean exit: True (forced: False, code: 0)
- screenshots: 7
- failures: none recorded

- service_start passed=True {"State":"Running","ProcessId":8272,"PathName":"\"C:\\Program Files\\USB Bridge Desk\\resources\\host\\usbipd.exe\" server"}
- tcp_3240_listener passed=True [{"LocalAddress":"::","LocalPort":3240,"OwningProcess":8272}]
- host_device_enumeration passed=True {"exitCode":0,"devices":0}
- usbip_devlist_handshake passed=True {"endpoint":"127.0.0.1:3240","version":"0x0111","request":"OP_REQ_DEVLIST","reply":"OP_REP_DEVLIST","status":0,"exports":0}
- installed_client_loopback_list passed=True {"exitCode":0,"exports":0,"endpoint":"127.0.0.1:3240"}
- client_virtual_ports passed=True {"exitCode":0,"importedPorts":0}
- restore_service passed=True {"restored":"Stopped"}
- 01-overview.png (overview) sha256=5161A11DFAAFD0A78EC2BBAF810E7DF1746BCFA8AE0354989767E5689A137E0F
- 02-local.png (host) sha256=2FD656BABF6979896218EBE30E9D89214717F5A1B21E5C53BAE87D4415FA4494
- 03-remote.png (client) sha256=566C69BBF8DD48DDF0B14221C33D8D114348006241FEBCC10F57E3DB71513BAE
- 04-mappings.png (mappings) sha256=F62E74443DCD273C91256D2F0B9F45767DD90A86F8F50EC9331FE7469FFA0A0A
- 05-dark-acrylic.png (settings dark / acrylic / desktop blend unverified) sha256=9F48A8A020B36E9A2CA0F586A597B4D0B1B842F0A720CA9C43D00B10F964CC32
- 06-dark-overview.png (overview dark / acrylic / desktop blend unverified) sha256=7259149D901D327883BA13CEA29BD2C7D4AF3A783B4933E2A3B0AED711B02A53
- 07-tray.png (visible application tray menu) sha256=64161BD97DC676F5DA6BFF5377CCD09CA16FBDE1800FBD18FFE9BC4D1EB55F1A
