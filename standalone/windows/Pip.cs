using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace PipDesktop {
 class UpdateClient:WebClient {protected override WebRequest GetWebRequest(Uri uri){WebRequest r=base.GetWebRequest(uri);r.Timeout=8000;HttpWebRequest h=r as HttpWebRequest;if(h!=null)h.ReadWriteTimeout=8000;return r;}}
 static class Program {
  [STAThread] static void Main(string[] args) {
   using (Mutex mutex = new Mutex(false, "Local\\PipDesktopCompanion")) {
    bool acquired=false; try { acquired=mutex.WaitOne(0,false); } catch(AbandonedMutexException) { acquired=true; }
    if(!acquired) return;
    Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
    try { Application.Run(new PetForm()); }
    catch(Exception e) { File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"pip-error.log"),DateTime.Now+" "+e+Environment.NewLine); }
    finally { mutex.ReleaseMutex(); }
   }
  }
 }
 public partial class PetForm : Form {
  public const string AppVersion="1.2.0";
  const string SettingsPath=@"Software\PipDesktop";
  readonly string settingsSubkey;bool cleaned=false;
  const int CW=192, CH=208;
  Bitmap atlas; System.Windows.Forms.Timer timer; NotifyIcon tray; ContextMenuStrip menu;
  Random random=new Random(); int row=0, frame=0, frameAge=0, stateAge=0, stateLimit=4000;
  int direction=1; bool paused=false, dragging=false, reported=false, doubleTap=false; Point dragStart, mouseStart;
  int renderedRow=-1, renderedFrame=-1, renderedLeft=int.MinValue, renderedTop=int.MinValue, renderedWidth=0;
  int scale=144; DateTime last=DateTime.UtcNow; Rectangle work;
  int speed=55,activity=1;bool autoFullscreen=true,checking=false,sleeping=false;
  DateTime touched=DateTime.UtcNow,happyUntil=DateTime.MinValue,playUntil=DateTime.MinValue,quietUntil=DateTime.MinValue,screenCheck=DateTime.MinValue,saved=DateTime.UtcNow,strokeTime=DateTime.MinValue;
  bool fullscreen=false;Point strokePoint;double strokes=0;
  public static bool CoversScreen(Rectangle window,Rectangle monitor){return window.Left<=monitor.Left+2&&window.Top<=monitor.Top+2&&window.Right>=monitor.Right-2&&window.Bottom>=monitor.Bottom-2;}
  public static int LookIndex(double dx,double dy){double a=Math.Atan2(dx,-dy)*180/Math.PI;if(a<0)a+=360;return ((int)Math.Round(a/22.5))%16;}
  public static bool ShouldSleep(double seconds){return seconds>=90;}
  public static bool Newer(string remote){Version v;return Version.TryParse(remote,out v)&&v>new Version(AppVersion);}
  int[][] durations={new[]{280,110,110,140,140,320},new[]{120,120,120,120,120,120,120,220},new[]{120,120,120,120,120,120,120,220},new[]{140,140,140,280},new[]{140,140,140,140,280},new[]{140,140,140,140,140,140,140,240},new[]{150,150,150,150,150,260},new[]{120,120,120,120,120,220},new[]{150,150,150,150,150,260}};
  protected override bool ShowWithoutActivation { get { return true; } }
  protected override CreateParams CreateParams { get { CreateParams p=base.CreateParams; p.ExStyle|=0x00080000|0x08000000|0x00000080; return p; } }
  public PetForm():this(SettingsPath,true){}
  public PetForm(string settingsKey,bool showTray) {
   settingsSubkey=settingsKey;
   Text="Пип — питомец рабочего стола"; FormBorderStyle=FormBorderStyle.None; ShowInTaskbar=false; TopMost=true;
   StartPosition=FormStartPosition.Manual;
   using(Stream s=Assembly.GetExecutingAssembly().GetManifestResourceStream("pip.atlas")) { using(Bitmap decoded=new Bitmap(s)){atlas=new Bitmap(decoded);} }
   work=Screen.PrimaryScreen.WorkingArea;
   using(RegistryKey k=Registry.CurrentUser.OpenSubKey(settingsSubkey)){if(k!=null){scale=Math.Max(80,Math.Min(240,Convert.ToInt32(k.GetValue("Size",144))));speed=Math.Max(15,Math.Min(140,Convert.ToInt32(k.GetValue("Speed",55))));activity=Math.Max(0,Math.Min(2,Convert.ToInt32(k.GetValue("Activity",1))));TopMost=Convert.ToInt32(k.GetValue("Topmost",1))==1;autoFullscreen=Convert.ToInt32(k.GetValue("Fullscreen",1))==1;}}
   Width=scale;Height=scale*CH/CW;Left=work.Right-Width-70;Top=work.Bottom-Height;
   using(RegistryKey k=Registry.CurrentUser.OpenSubKey(settingsSubkey)){if(k!=null){Left=Convert.ToInt32(k.GetValue("X",Left));Top=Convert.ToInt32(k.GetValue("Y",Top));}}
   Clamp();
   menu=new ContextMenuStrip();
   menu.Items.Add("Помахать",null,delegate{Touch();SetState(3,700);});
   menu.Items.Add("Прыгнуть",null,delegate{Touch();SetState(4,850);});
   menu.Items.Add("Погладить",null,delegate{Pet();});
   menu.Items.Add("Поиграть",null,delegate{Touch();playUntil=DateTime.UtcNow.AddSeconds(12);SetState(4,850);});
   menu.Items.Add("Уложить спать",null,delegate{happyUntil=DateTime.MinValue;playUntil=DateTime.MinValue;touched=DateTime.UtcNow.AddMinutes(-2);sleeping=true;});
   ToolStripMenuItem quiet=new ToolStripMenuItem("Не мешать");foreach(int minutes in new[]{15,30,60}){int m=minutes;quiet.DropDownItems.Add(m+" минут",null,delegate{quietUntil=DateTime.UtcNow.AddMinutes(m);Hide();});}quiet.DropDownItems.Add("Показать сейчас",null,delegate{quietUntil=DateTime.MinValue;Touch();});menu.Items.Add(quiet);
   ToolStripMenuItem pause=new ToolStripMenuItem("Пауза"); pause.CheckOnClick=true; pause.CheckedChanged+=delegate{paused=pause.Checked;}; menu.Items.Add(pause);
   ToolStripMenuItem top=new ToolStripMenuItem("Поверх окон"); top.Checked=TopMost; top.CheckOnClick=true; top.CheckedChanged+=delegate{TopMost=top.Checked;SaveSettings();}; menu.Items.Add(top);
   ToolStripMenuItem fs=new ToolStripMenuItem("Прятаться в полноэкранных приложениях");fs.Checked=autoFullscreen;fs.CheckOnClick=true;fs.CheckedChanged+=delegate{autoFullscreen=fs.Checked;SaveSettings();};menu.Items.Add(fs);
   ToolStripMenuItem sizes=new ToolStripMenuItem("Размер");
   foreach(int sz in new[]{112,144,192}) { int picked=sz; sizes.DropDownItems.Add(sz+" px",null,delegate{ResizePet(picked);}); } menu.Items.Add(sizes);
   ToolStripMenuItem pace=new ToolStripMenuItem("Скорость");int[] speeds={25,55,100};string[] speedNames={"Медленно","Обычно","Быстро"};for(int i=0;i<speeds.Length;i++){int v=speeds[i];pace.DropDownItems.Add(speedNames[i],null,delegate{speed=v;SaveSettings();});}menu.Items.Add(pace);
   ToolStripMenuItem energy=new ToolStripMenuItem("Активность");string[] energyNames={"Спокойный","Обычный","Игривый"};for(int i=0;i<3;i++){int v=i;energy.DropDownItems.Add(energyNames[i],null,delegate{activity=v;SaveSettings();});}menu.Items.Add(energy);
   ToolStripMenuItem startup=new ToolStripMenuItem("Запускать с Windows"); startup.Checked=HasStartup(); startup.CheckOnClick=true;
   startup.CheckedChanged+=delegate{using(RegistryKey k=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")){if(startup.Checked)k.SetValue("PipDesktop","\""+Application.ExecutablePath+"\"");else k.DeleteValue("PipDesktop",false);}}; menu.Items.Add(startup);
   menu.Items.Add("Вернуть к краю экрана",null,delegate{work=Screen.PrimaryScreen.WorkingArea;Left=work.Right-Width-70;Top=work.Bottom-Height;SetState(3,700);RenderFrame(3,0);});
   menu.Items.Add("Проверить обновления…",null,delegate{CheckUpdates();});
   menu.Items.Add("О Пипе "+AppVersion,null,delegate{MessageBox.Show("Пип "+AppVersion+"\nСон, поглаживание, игры и сохранение настроек.\nПроверка обновлений обращается к GitHub только по кнопке.","Пип");});
   menu.Items.Add("Выход",null,delegate{Close();}); ContextMenuStrip=menu;
   tray=new NotifyIcon(); tray.Text="Пип — нажми на лисёнка или открой меню"; tray.ContextMenuStrip=menu;
   using(Stream s=Assembly.GetExecutingAssembly().GetManifestResourceStream("pip.icon")){tray.Icon=new Icon(s);} tray.Visible=showTray;
   tray.DoubleClick+=delegate{quietUntil=DateTime.MinValue;Touch();Show();SetState(3,700);};
   MouseDown+=OnPetDown; MouseMove+=OnPetMove; MouseUp+=OnPetUp;
   timer=new System.Windows.Forms.Timer(); timer.Interval=40; timer.Tick+=Tick;
   Shown+=delegate{RenderFrame(0,0);timer.Start();};
   SystemEvents.DisplaySettingsChanged+=DisplayChanged;
   InitV12(showTray);
  }
  bool HasStartup(){using(RegistryKey k=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")){return k!=null&&k.GetValue("PipDesktop")!=null;}}
  void SaveSettings(){using(RegistryKey k=Registry.CurrentUser.CreateSubKey(settingsSubkey)){k.SetValue("X",Left);k.SetValue("Y",Top);k.SetValue("Size",scale);k.SetValue("Speed",speed);k.SetValue("Activity",activity);k.SetValue("Topmost",TopMost?1:0);k.SetValue("Fullscreen",autoFullscreen?1:0);SaveV12(k);}saved=DateTime.UtcNow;}
  void Touch(){touched=DateTime.UtcNow;sleeping=false;}
  void Pet(){Touch();happyUntil=DateTime.UtcNow.AddSeconds(5);SetState(6,5000);}
  bool IsFullscreen(){IntPtr h=GetForegroundWindow();if(h==IntPtr.Zero||h==Handle)return false;StringBuilder name=new StringBuilder(128);GetClassName(h,name,128);if(name.ToString()=="Progman"||name.ToString()=="WorkerW"||name.ToString()=="Shell_TrayWnd")return false;RECT r;if(!GetWindowRect(h,out r))return false;return CoversScreen(Rectangle.FromLTRB(r.l,r.t,r.r,r.b),Screen.FromHandle(h).Bounds);}
  void CheckUpdates(){if(checking)return;checking=true;ThreadPool.QueueUserWorkItem(delegate{string message;bool newer=false;try{ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;using(WebClient client=new UpdateClient()){client.Headers["User-Agent"]="Pip/"+AppVersion;string json=client.DownloadString("https://raw.githubusercontent.com/Ahdrej545/pip-pet/main/standalone/version.json");Match m=Regex.Match(json,"\"version\"\\s*:\\s*\"([0-9.]+)\"");if(!m.Success)throw new Exception();newer=Newer(m.Groups[1].Value);message=newer?"Доступен Пип "+m.Groups[1].Value+". Открыть страницу скачивания?":"У тебя актуальная версия Пипа ("+AppVersion+").";}}catch{message="Не удалось проверить обновления. Пип продолжит работать без интернета.";}if(!IsDisposed)BeginInvoke((MethodInvoker)delegate{checking=false;if(newer){if(MessageBox.Show(message,"Пип",MessageBoxButtons.YesNo)==DialogResult.Yes)Process.Start(new ProcessStartInfo("https://github.com/Ahdrej545/pip-pet/tree/main/standalone"){UseShellExecute=true});}else MessageBox.Show(message,"Пип");});});}
  void DisplayChanged(object s,EventArgs e){if(!IsDisposed)BeginInvoke((MethodInvoker)delegate{Clamp();});}
  void ResizePet(int sz){int bottom=Top+Height;scale=sz;Width=scale;Height=scale*CH/CW;Top=bottom-Height;Clamp();SaveSettings();RenderFrame(row,frame);}
  void Clamp(){work=Screen.FromPoint(new Point(Left+Width/2,Top+Height/2)).WorkingArea;Left=Math.Max(work.Left,Math.Min(work.Right-Width,Left));Top=Math.Max(work.Top,Math.Min(work.Bottom-Height,Top));}
  void SetState(int r,int limit){row=r;frame=0;frameAge=0;stateAge=0;stateLimit=limit;}
  void OnPetDown(object s,MouseEventArgs e){if(e.Button!=MouseButtons.Left)return;Touch();doubleTap=e.Clicks>1;dragging=true;dragStart=Location;mouseStart=Cursor.Position;Capture=true;}
  void OnPetMove(object s,MouseEventArgs e){if(!dragging){DateTime now=DateTime.UtcNow;if((now-strokeTime).TotalMilliseconds>800)strokes=0;else strokes+=Math.Abs(e.X-strokePoint.X)+Math.Abs(e.Y-strokePoint.Y);strokeTime=now;strokePoint=e.Location;if(strokes>70){strokes=0;Pet();}return;}Point m=Cursor.Position;Location=new Point(dragStart.X+m.X-mouseStart.X,dragStart.Y+m.Y-mouseStart.Y);RenderFrame(0,0);}
  void OnPetUp(object s,MouseEventArgs e){if(!dragging)return;dragging=false;Capture=false;Clamp();SaveSettings();Point m=Cursor.Position;bool moved=Math.Abs(m.X-mouseStart.X)+Math.Abs(m.Y-mouseStart.Y)>10;if(doubleTap&&!moved)SetState(4,850);else SetState(3,700);RenderFrame(row,0);}
  void Tick(object s,EventArgs e){
   DateTime now=DateTime.UtcNow;int dt=Math.Min(150,Math.Max(1,(int)(now-last).TotalMilliseconds));last=now;
   if(now>=screenCheck){fullscreen=autoFullscreen&&IsFullscreen();screenCheck=now.AddMilliseconds(700);}
   bool hidden=now<quietUntil||fullscreen||sessionLocked;if(hidden){SyncDecor(true);if(Visible)Hide();return;}if(!Visible)Show();
   if((now-saved).TotalSeconds>=10)SaveSettings();
   if(dragging)return;
   if(paused){RenderFrame(0,0);return;}
   if(UpdateV12(dt,now))return;
   sleeping=ShouldSleep((now-touched).TotalSeconds);if(sleeping){tray.Text="Пип спит — погладь, чтобы разбудить";RenderFrame(0,3);return;}
   if(now<happyUntil){tray.Text="Пип радуется поглаживанию";RenderFrame(6,3);return;}
   tray.Text="Пип "+AppVersion+" — нажми или погладь лисёнка";
   stateAge+=dt;frameAge+=dt;
   if(stateAge>=stateLimit){if(now<playUntil){int game=random.Next(4);if(game==0)SetState(4,850);else if(game==1)SetState(3,700);else{direction=game==2?1:-1;SetState(direction>0?1:2,1000);}}else{int choice=random.Next(activity==0?18:activity==2?8:12);if(choice<4||choice>10)SetState(0,random.Next(3000,7000));else if(choice<7){direction=random.Next(2)==0?-1:1;SetState(direction>0?1:2,random.Next(2000,4800));}else if(choice==7)SetState(4,850);else if(choice==8)SetState(3,700);else if(choice==9)SetState(6,3500);else SetState(7,2200);}}
   if(row==1||row==2){work=Screen.FromControl(this).WorkingArea;int dx=Math.Max(1,dt*speed/1000);Left+=direction*dx;if(Left<work.Left||Left+Width>work.Right){direction=-direction;Left=Math.Max(work.Left,Math.Min(work.Right-Width,Left));SetState(direction>0?1:2,stateLimit);}}
   while(frameAge>=durations[row][frame]){frameAge-=durations[row][frame];frame=(frame+1)%durations[row].Length;}
   int drawRow=row,drawFrame=frame;
   if(row==0){Point p=Cursor.Position;double dx=p.X-(Left+Width*.55),dy=p.Y-(Top+Height*.40),distance=Math.Sqrt(dx*dx+dy*dy);if(distance>35&&distance<330){int i=LookIndex(dx,dy);drawRow=9+i/8;drawFrame=i%8;}}
   RenderFrame(drawRow,drawFrame);
  }
  void RenderFrame(int r,int f){if(!IsHandleCreated||IsDisposed)return;
   if(r==renderedRow&&f==renderedFrame&&Left==renderedLeft&&Top==renderedTop&&Width==renderedWidth)return;
   using(Bitmap b=new Bitmap(Width,Height,PixelFormat.Format32bppPArgb)){
    using(Graphics g=Graphics.FromImage(b)){g.Clear(Color.Transparent);g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.DrawImage(atlas,new Rectangle(0,0,Width,Height),new Rectangle(f*CW,r*CH,CW,CH),GraphicsUnit.Pixel);DrawAccessory(g,r,f);}
    IntPtr screen=GetDC(IntPtr.Zero),mem=CreateCompatibleDC(screen),hb=IntPtr.Zero,old=IntPtr.Zero;
    try{hb=b.GetHbitmap(Color.FromArgb(0));old=SelectObject(mem,hb);POINT pt=new POINT(Left,Top),src=new POINT(0,0);SIZE size=new SIZE(Width,Height);BLENDFUNCTION blend=new BLENDFUNCTION();blend.SourceConstantAlpha=255;blend.AlphaFormat=1;bool ok=UpdateLayeredWindow(Handle,screen,ref pt,ref size,mem,ref src,0,ref blend,2);if(!reported){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"pip-status.txt"),"LayeredWindow="+ok+"\nVisible="+IsWindowVisible(Handle)+"\nPosition="+Left+","+Top+"\nSize="+Width+"x"+Height+"\nVersion="+AppVersion+"\n");reported=true;}if(ok){renderedRow=r;renderedFrame=f;renderedLeft=Left;renderedTop=Top;renderedWidth=Width;}}
    finally{if(old!=IntPtr.Zero)SelectObject(mem,old);if(hb!=IntPtr.Zero)DeleteObject(hb);DeleteDC(mem);ReleaseDC(IntPtr.Zero,screen);}
   }
  }
  void Cleanup(){if(cleaned)return;cleaned=true;CleanupV12();if(timer!=null){timer.Stop();timer.Dispose();}if(tray!=null){tray.Visible=false;tray.Dispose();}if(atlas!=null)atlas.Dispose();SystemEvents.DisplaySettingsChanged-=DisplayChanged;}
  protected override void OnFormClosed(FormClosedEventArgs e){SaveSettings();Cleanup();base.OnFormClosed(e);}
  protected override void Dispose(bool disposing){if(disposing)Cleanup();base.Dispose(disposing);}
  [StructLayout(LayoutKind.Sequential)]struct RECT{public int l,t,r,b;}
  [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr h,out RECT r);
  [DllImport("user32.dll",CharSet=CharSet.Auto)]static extern int GetClassName(IntPtr h,StringBuilder name,int max);
  [StructLayout(LayoutKind.Sequential)]struct POINT{public int x,y;public POINT(int a,int b){x=a;y=b;}}
  [StructLayout(LayoutKind.Sequential)]struct SIZE{public int cx,cy;public SIZE(int a,int b){cx=a;cy=b;}}
  [StructLayout(LayoutKind.Sequential,Pack=1)]struct BLENDFUNCTION{public byte BlendOp,BlendFlags,SourceConstantAlpha,AlphaFormat;}
  [DllImport("user32.dll")]static extern bool UpdateLayeredWindow(IntPtr hwnd,IntPtr dc,ref POINT dest,ref SIZE size,IntPtr src,ref POINT origin,int key,ref BLENDFUNCTION blend,int flags);
  [DllImport("user32.dll")]static extern IntPtr GetDC(IntPtr hwnd);
  [DllImport("user32.dll")]static extern int ReleaseDC(IntPtr hwnd,IntPtr dc);
  [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr hwnd);
  [DllImport("gdi32.dll")]static extern IntPtr CreateCompatibleDC(IntPtr dc);
  [DllImport("gdi32.dll")]static extern bool DeleteDC(IntPtr dc);
  [DllImport("gdi32.dll")]static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
  [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr obj);
 }
}
