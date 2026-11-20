#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-only
"""Build the bundled Windows service and CLI from the pinned upstream source."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
from zipfile import ZipFile

root = Path(__file__).resolve().parents[1]
archive = root / 'notices/usbipd-win-5.3.0-source.zip'
with archive.open('rb') as stream:
    if hashlib.file_digest(stream, 'sha256').hexdigest() != 'ba590c5f73a209683e8285a6fe6ff852f696bd433893cd6081e6d4223c893da4':
        raise ValueError('Unexpected usbipd source archive')
work = root / 'build/host-source'
shutil.rmtree(work, ignore_errors=True)
with ZipFile(archive) as upstream:
    upstream.extractall(work)
project = work / 'usbipd-win-5.3.0'
props = project / 'Directory.Build.props'
# The upstream version generator expects a Git checkout. This input is a tag archive.
props.write_text(props.read_text(encoding='utf-8-sig').replace(
    '<PackageReference Include="Dorssel.GitVersion.MsBuild" PrivateAssets="all" />', ''),
    encoding='utf-8')
(project/'Usbipd/GitVersionInformation.cs').write_text(
    'internal static class GitVersionInformation {\n'
    '    public const string MajorMinorPatch = "5.3.0";\n'
    '    public const string InformationalVersion = "5.3.0+usbbridgedesk.r3";\n'
    '}\n', encoding='utf-8')
config_file = project/'global.json'
config = json.loads(config_file.read_text())
config['sdk'] = {'version':'10.0.404','rollForward':'disable'}
config_file.write_text(json.dumps(config), encoding='utf-8')
dest = root/'build/host-win-x64'
shutil.rmtree(dest, ignore_errors=True)
subprocess.run([os.environ.get('DOTNET','dotnet'),'publish','Usbipd/Usbipd.csproj',
    '-c','Release','-r','win-x64','--self-contained','false','-o',str(dest),
    '-p:MainTargetFramework=net10.0','-p:Platform=x64','-p:PublishAot=false',
    '-p:Version=5.3.0','-p:Copyright=Copyright (C) Frans van Dorsselaer',
    '-p:GenerateDependencyFile=true','-p:DebugType=None','-p:TreatWarningsAsErrors=false'],
    cwd=project, check=True)
for filename in ('usbipd.exe','usbipd.dll','usbipd.runtimeconfig.json','usb.ids','Drivers/VBoxUSB.inf'):
    if not (dest/filename).is_file():
        raise FileNotFoundError(dest/filename)
