"""Build Pip locally. Signing credentials remain in ignored .build/."""
from pathlib import Path
import os, subprocess, zipfile, shutil, sys, secrets

root=Path(__file__).resolve().parent
build=Path(os.environ.get('PIP_BUILD_DIR',str(root/'.build'))).resolve();build.mkdir(parents=True,exist_ok=True)
def run(*args):subprocess.run([str(x) for x in args],check=True,env=env)
env=dict(os.environ)
if '--android-only' not in sys.argv:
    if os.name!='nt':raise SystemExit('Windows build requires Windows; use --android-only for Android.')
    csc=Path(os.environ.get('WINDIR','C:/Windows'))/'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
    run(csc,'/nologo','/target:winexe','/optimize+','/platform:anycpu','/out:'+str(root/'Pip.exe'),'/win32icon:'+str(root/'windows/pip.ico'),'/resource:'+str(root/'windows/spritesheet.png')+',pip.atlas','/resource:'+str(root/'windows/pip.ico')+',pip.icon','/resource:'+str(root/'windows/accessory-anchors.json')+',pip.anchors','/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Web.Extensions.dll',*sorted((root/'windows').glob('*.cs')))
if '--windows-only' in sys.argv:sys.exit(0)
sdk_value=os.environ.get('ANDROID_HOME') or os.environ.get('ANDROID_SDK_ROOT')
sdk=Path(sdk_value) if sdk_value else Path(os.environ.get('LOCALAPPDATA',str(Path.home())))/'Android/Sdk'
tools=sdk/'build-tools/36.0.0';androidjar=sdk/'platforms/android-34/android.jar'
jdk_value=os.environ.get('JAVA_HOME')
if not jdk_value:
    installed=list(Path('C:/Program Files/Eclipse Adoptium').glob('jdk-17*'))
    if installed:jdk_value=str(installed[0])
if not jdk_value:
    javac=shutil.which('javac')
    if not javac:raise SystemExit('Install JDK 17 and set JAVA_HOME.')
    jdk_value=str(Path(javac).resolve().parent.parent)
jdk=Path(jdk_value);exe='.exe' if os.name=='nt' else ''
env['JAVA_HOME']=str(jdk);env['PATH']=str(jdk/'bin')+os.pathsep+env.get('PATH','')
for p in [tools,androidjar,jdk/'bin'/('javac'+exe)]:
    if not p.exists():raise SystemExit('Missing build dependency: '+str(p))
generated=build/'generated';classes=build/'classes';dex=build/'dex'
for p in [generated,classes,dex]:
    if not p.resolve().is_relative_to(build):raise SystemExit('Unsafe intermediate directory')
    if p.exists():shutil.rmtree(p)
    p.mkdir()
run(tools/('aapt2'+exe),'compile','--dir',root/'android/res','-o',build/'resources.zip')
run(tools/('aapt2'+exe),'link','-o',build/'resources.apk','-I',androidjar,'--manifest',root/'android/AndroidManifest.xml','--java',generated,'-A',root/'android/assets',build/'resources.zip')
sources=list((root/'android/src').rglob('*.java'))+list(generated.rglob('*.java'))
run(jdk/'bin'/('javac'+exe),'-encoding','UTF-8','-source','8','-target','8','-classpath',androidjar,'-d',classes,*sources)
with zipfile.ZipFile(build/'classes.jar','w',zipfile.ZIP_DEFLATED) as z:
    for p in classes.rglob('*.class'):z.write(p,p.relative_to(classes).as_posix())
run(jdk/'bin'/('java'+exe),'-cp',tools/'lib/d8.jar','com.android.tools.r8.D8','--lib',androidjar,'--min-api','30','--output',dex,build/'classes.jar')
shutil.copy2(build/'resources.apk',build/'unsigned.apk')
with zipfile.ZipFile(build/'unsigned.apk','a',zipfile.ZIP_DEFLATED) as z:z.write(dex/'classes.dex','classes.dex')
run(tools/('zipalign'+exe),'-f','-p','4',build/'unsigned.apk',build/'aligned.apk')
key=build/'pip-release.jks';passwordfile=build/'keystore-password.txt'
if not key.exists():
    passwordfile.write_text(secrets.token_urlsafe(32)+'\n',encoding='utf-8')
    run(jdk/'bin'/('keytool'+exe),'-genkeypair','-keystore',key,'-storepass:file',passwordfile,'-keypass:file',passwordfile,'-alias','pip','-keyalg','RSA','-keysize','2048','-validity','10000','-dname','CN=Pip Local Build')
if not passwordfile.exists():raise SystemExit('Missing password file for existing signing key.')
run(jdk/'bin'/('java'+exe),'-jar',tools/'lib/apksigner.jar','sign','--ks',key,'--ks-pass','file:'+str(passwordfile),'--out',build/'Pip-signed.apk',build/'aligned.apk')
run(jdk/'bin'/('java'+exe),'-jar',tools/'lib/apksigner.jar','verify','--verbose',build/'Pip-signed.apk')
shutil.copy2(build/'Pip-signed.apk',root/'Pip.apk')
print('Built Pip.apk. Keep your local signing key private.')
