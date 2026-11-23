# R3 1.1.0 Windows test - run 89

- build: success
- silent install: success
- installed backend: success
- desktop capture: success
- installer: SIZE=45275977 | SHA256=98C2518C74B58685D097E8AEA3E1DADA6ED43287577C9C7711505006406A12ED | INSTALL_EXIT=3010
- driver exit: DRIVER_EXIT=0
- alive 30s: True
- clean exit: True (forced: False, code: 0)
- screenshots: 7
- failures: none recorded

- service_start passed=True {"State":"Running","ProcessId":6168,"PathName":"\"C:\\Program Files\\USB Bridge Desk\\resources\\host\\usbipd.exe\" server"}
- tcp_3240_listener passed=True [{"LocalAddress":"::","LocalPort":3240,"OwningProcess":6168}]
- host_device_enumeration passed=True {"exitCode":0,"devices":0}
- usbip_devlist_handshake passed=True {"endpoint":"127.0.0.1:3240","version":"0x0111","request":"OP_REQ_DEVLIST","reply":"OP_REP_DEVLIST","status":0,"exports":0}
- installed_client_loopback_list passed=True {"exitCode":0,"exports":0,"endpoint":"127.0.0.1:3240"}
- client_virtual_ports passed=True {"exitCode":0,"importedPorts":0}
- restore_service passed=True {"restored":"Stopped"}
- 01-overview.png (overview) sha256=5161A11DFAAFD0A78EC2BBAF810E7DF1746BCFA8AE0354989767E5689A137E0F
- 02-local.png (host) sha256=4B5F43A519AEE155D77AA1F6BFDFC2EE432D101F127B0FF97F2783708BC921DC
- 03-remote.png (client) sha256=566C69BBF8DD48DDF0B14221C33D8D114348006241FEBCC10F57E3DB71513BAE
- 04-mappings.png (mappings) sha256=F62E74443DCD273C91256D2F0B9F45767DD90A86F8F50EC9331FE7469FFA0A0A
- 05-dark-acrylic.png (settings dark / acrylic / desktop blend unverified) sha256=9F48A8A020B36E9A2CA0F586A597B4D0B1B842F0A720CA9C43D00B10F964CC32
- 06-dark-overview.png (overview dark / acrylic / desktop blend unverified) sha256=4E6BBE04B39B57F05F63D1DD5F94142463B3765717C19106D325773602EEAC28
- 07-tray.png (visible application tray menu) sha256=B14295492D356AFAAA4A8DFC20739575909790368BDE1A8436F691A2B8358FBE
