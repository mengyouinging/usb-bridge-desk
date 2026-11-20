# R3 1.1.0 Windows test - run 8

- build: success
- silent install: success
- installed backend: success
- desktop capture: success
- installer: SIZE=45287099 | SHA256=1F4BA29CD1D6AEB96F8489744929703221A77EED1CF8D4AEBB1282CFAEEAC963 | INSTALL_EXIT=3010
- driver exit: DRIVER_EXIT=0
- alive 30s: True
- clean exit: True (forced: False, code: 0)
- screenshots: 7
- failures: none recorded

- service_start passed=True {"State":"Running","ProcessId":6496,"PathName":"\"C:\\Program Files\\USB Bridge Desk\\resources\\host\\usbipd.exe\" server"}
- tcp_3240_listener passed=True [{"LocalAddress":"::","LocalPort":3240,"OwningProcess":6496}]
- host_device_enumeration passed=True {"exitCode":0,"devices":0}
- usbip_devlist_handshake passed=True {"endpoint":"127.0.0.1:3240","version":"0x0111","request":"OP_REQ_DEVLIST","reply":"OP_REP_DEVLIST","status":0,"exports":0}
- installed_client_loopback_list passed=True {"exitCode":0,"exports":0,"endpoint":"127.0.0.1:3240"}
- client_virtual_ports passed=True {"exitCode":0,"importedPorts":0}
- restore_service passed=True {"restored":"Stopped"}
- 01-overview.png (overview) sha256=5161A11DFAAFD0A78EC2BBAF810E7DF1746BCFA8AE0354989767E5689A137E0F
- 02-local.png (host) sha256=A960769F3A44241CAC1F56C8510155E096EE1B10853C39AA11524760BFC6AB7D
- 03-remote.png (client) sha256=566C69BBF8DD48DDF0B14221C33D8D114348006241FEBCC10F57E3DB71513BAE
- 04-mappings.png (mappings) sha256=F62E74443DCD273C91256D2F0B9F45767DD90A86F8F50EC9331FE7469FFA0A0A
- 05-dark-acrylic.png (settings dark / acrylic / desktop blend unverified) sha256=EF12FC34492EA516C48F8B85DCD7C7F6562568AE195822B865D1A9360577B119
- 06-dark-overview.png (overview dark / acrylic / desktop blend unverified) sha256=BC818722D82E7AD308372F863DCA99368E9CD5E5D8CC1B40115E71B954167A29
- 07-tray.png (visible application tray menu) sha256=BC1C11A17544DB6589F0538177165F4673AF885E20ADE08E36A3A2F89F3993B1
