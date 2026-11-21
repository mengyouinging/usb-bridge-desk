# R3 1.1.0 Windows test - run 10

- build: success
- silent install: success
- installed backend: success
- desktop capture: success
- installer: SIZE=45270984 | SHA256=B1EC973A30184ED4C1C7F78A27615A7A8F531EEA60D241EDC4454C2FA9B8F199 | INSTALL_EXIT=3010
- driver exit: DRIVER_EXIT=0
- alive 30s: True
- clean exit: True (forced: False, code: 0)
- screenshots: 7
- failures: none recorded

- service_start passed=True {"State":"Running","ProcessId":3144,"PathName":"\"C:\\Program Files\\USB Bridge Desk\\resources\\host\\usbipd.exe\" server"}
- tcp_3240_listener passed=True [{"LocalAddress":"::","LocalPort":3240,"OwningProcess":3144}]
- host_device_enumeration passed=True {"exitCode":0,"devices":0}
- usbip_devlist_handshake passed=True {"endpoint":"127.0.0.1:3240","version":"0x0111","request":"OP_REQ_DEVLIST","reply":"OP_REP_DEVLIST","status":0,"exports":0}
- installed_client_loopback_list passed=True {"exitCode":0,"exports":0,"endpoint":"127.0.0.1:3240"}
- client_virtual_ports passed=True {"exitCode":0,"importedPorts":0}
- restore_service passed=True {"restored":"Stopped"}
- 01-overview.png (overview) sha256=5161A11DFAAFD0A78EC2BBAF810E7DF1746BCFA8AE0354989767E5689A137E0F
- 02-local.png (host) sha256=78D78B38531FF62080CB42E83EDF8649FB4E7DDD3D1FFF23D7E62DAEFD8B3D18
- 03-remote.png (client) sha256=566C69BBF8DD48DDF0B14221C33D8D114348006241FEBCC10F57E3DB71513BAE
- 04-mappings.png (mappings) sha256=F62E74443DCD273C91256D2F0B9F45767DD90A86F8F50EC9331FE7469FFA0A0A
- 05-dark-acrylic.png (settings dark / acrylic / desktop blend unverified) sha256=9F48A8A020B36E9A2CA0F586A597B4D0B1B842F0A720CA9C43D00B10F964CC32
- 06-dark-overview.png (overview dark / acrylic / desktop blend unverified) sha256=6A1F5B0CE02F8B3F25BBAE917B9733A4FCF8D31C8C07F1636969CB58FC6B159A
- 07-tray.png (visible application tray menu) sha256=0DC59D4A806ACD8BD7A02A310CBC5B8FF6C6C2BD4C6B0AA8D5B170DA2429839F
