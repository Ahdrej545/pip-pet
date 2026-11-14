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

namespace PipDesktop {
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
 public class PetForm : Form {
  const int CW=192, CH=208;
  Bitmap atlas; System.Windows.Forms.Timer timer; NotifyIcon tray; ContextMenuStrip menu;
  Random random=new Random(); int row=0, frame=0, frameAge=0, stateAge=0, stateLimit=4000;
  int direction=1; bool paused=false, dragging=false, reported=false, doubleTap=false; Point dragStart, mouseStart;
  int renderedRow=-1, renderedFrame=-1, renderedLeft=int.MinValue, renderedTop=int.MinValue, renderedWidth=0;
  int scale=144; DateTime last=DateTime.UtcNow; Rectangle work;
  int[][] durations={new[]{280,110,110,140,140,320},new[]{120,120,120,120,120,120,120,220},new[]{120,120,120,120,120,120,120,220},new[]{140,140,140,280},new[]{140,140,140,140,280},new[]{140,140,140,140,140,140,140,240},new[]{150,150,150,150,150,260},new[]{120,120,120,120,120,220},new[]{150,150,150,150,150,260}};
  protected override bool ShowWithoutActivation { get { return true; } }
  protected override CreateParams CreateParams { get { CreateParams p=base.CreateParams; p.ExStyle|=0x00080000|0x08000000|0x00000080; return p; } }
  public PetForm() {
   Text="Пип — питомец рабочего стола"; FormBorderStyle=FormBorderStyle.None; ShowInTaskbar=false; TopMost=true;
   StartPosition=FormStartPosition.Manual;
   using(Stream s=Assembly.GetExecutingAssembly().GetManifestResourceStream("pip.atlas")) { using(Bitmap decoded=new Bitmap(s)){atlas=new Bitmap(decoded);} }
   work=Screen.PrimaryScreen.WorkingArea; Width=scale; Height=scale*CH/CW; Left=work.Right-Width-70; Top=work.Bottom-Height;
   menu=new ContextMenuStrip();
   menu.Items.Add("Помахать",null,delegate{SetState(3,700);});
   menu.Items.Add("Прыгнуть",null,delegate{SetState(4,850);});
   ToolStripMenuItem pause=new ToolStripMenuItem("Пауза"); pause.CheckOnClick=true; pause.CheckedChanged+=delegate{paused=pause.Checked;}; menu.Items.Add(pause);
   ToolStripMenuItem top=new ToolStripMenuItem("Поверх окон"); top.Checked=true; top.CheckOnClick=true; top.CheckedChanged+=delegate{TopMost=top.Checked;}; menu.Items.Add(top);
   ToolStripMenuItem sizes=new ToolStripMenuItem("Размер");
   foreach(int sz in new[]{112,144,192}) { int picked=sz; sizes.DropDownItems.Add(sz+" px",null,delegate{ResizePet(picked);}); } menu.Items.Add(sizes);
   ToolStripMenuItem startup=new ToolStripMenuItem("Запускать с Windows"); startup.Checked=HasStartup(); startup.CheckOnClick=true;
   startup.CheckedChanged+=delegate{using(RegistryKey k=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")){if(startup.Checked)k.SetValue("PipDesktop","\""+Application.ExecutablePath+"\"");else k.DeleteValue("PipDesktop",false);}}; menu.Items.Add(startup);
   menu.Items.Add("Вернуть к краю экрана",null,delegate{work=Screen.PrimaryScreen.WorkingArea;Left=work.Right-Width-70;Top=work.Bottom-Height;SetState(3,700);RenderFrame(3,0);});
   menu.Items.Add("Выход",null,delegate{Close();}); ContextMenuStrip=menu;
   tray=new NotifyIcon(); tray.Text="Пип — нажми на лисёнка или открой меню"; tray.ContextMenuStrip=menu;
   using(Stream s=Assembly.GetExecutingAssembly().GetManifestResourceStream("pip.icon")){tray.Icon=new Icon(s);} tray.Visible=true;
   tray.DoubleClick+=delegate{Show(); SetState(3,700);};
   MouseDown+=OnPetDown; MouseMove+=OnPetMove; MouseUp+=OnPetUp;
   timer=new System.Windows.Forms.Timer(); timer.Interval=40; timer.Tick+=Tick;
   Shown+=delegate{RenderFrame(0,0);timer.Start();};
   SystemEvents.DisplaySettingsChanged+=DisplayChanged;
  }
  bool HasStartup(){using(RegistryKey k=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")){return k!=null&&k.GetValue("PipDesktop")!=null;}}
  void DisplayChanged(object s,EventArgs e){if(!IsDisposed)BeginInvoke((MethodInvoker)delegate{Clamp();});}
  void ResizePet(int sz){int bottom=Top+Height;scale=sz;Width=scale;Height=scale*CH/CW;Top=bottom-Height;Clamp();RenderFrame(row,frame);}
  void Clamp(){work=Screen.FromPoint(new Point(Left+Width/2,Top+Height/2)).WorkingArea;Left=Math.Max(work.Left,Math.Min(work.Right-Width,Left));Top=Math.Max(work.Top,Math.Min(work.Bottom-Height,Top));}
  void SetState(int r,int limit){row=r;frame=0;frameAge=0;stateAge=0;stateLimit=limit;}
  void OnPetDown(object s,MouseEventArgs e){if(e.Button!=MouseButtons.Left)return;doubleTap=e.Clicks>1;dragging=true;dragStart=Location;mouseStart=Cursor.Position;Capture=true;}
  void OnPetMove(object s,MouseEventArgs e){if(!dragging)return;Point m=Cursor.Position;Location=new Point(dragStart.X+m.X-mouseStart.X,dragStart.Y+m.Y-mouseStart.Y);RenderFrame(0,0);}
  void OnPetUp(object s,MouseEventArgs e){if(!dragging)return;dragging=false;Capture=false;Clamp();Point m=Cursor.Position;bool moved=Math.Abs(m.X-mouseStart.X)+Math.Abs(m.Y-mouseStart.Y)>10;if(doubleTap&&!moved)SetState(4,850);else SetState(3,700);RenderFrame(row,0);}
  void Tick(object s,EventArgs e){
   DateTime now=DateTime.UtcNow;int dt=Math.Min(150,Math.Max(1,(int)(now-last).TotalMilliseconds));last=now;
   if(dragging||!Visible)return;
   if(paused){RenderFrame(0,0);return;}
   stateAge+=dt;frameAge+=dt;
   if(stateAge>=stateLimit){int choice=random.Next(10);if(choice<4)SetState(0,random.Next(3000,7000));else if(choice<7){direction=random.Next(2)==0?-1:1;SetState(direction>0?1:2,random.Next(2000,4800));}else if(choice==7)SetState(4,850);else if(choice==8)SetState(3,700);else SetState(7,2200);}
   if(row==1||row==2){work=Screen.FromControl(this).WorkingArea;int dx=Math.Max(1,dt*55/1000);Left+=direction*dx;if(Left<work.Left||Left+Width>work.Right){direction=-direction;Left=Math.Max(work.Left,Math.Min(work.Right-Width,Left));SetState(direction>0?1:2,stateLimit);}}
   while(frameAge>=durations[row][frame]){frameAge-=durations[row][frame];frame=(frame+1)%durations[row].Length;}
   int drawRow=row,drawFrame=frame;
   if(row==0){Point p=Cursor.Position;double dx=p.X-(Left+Width*.55),dy=p.Y-(Top+Height*.40),distance=Math.Sqrt(dx*dx+dy*dy);if(distance>35&&distance<330){double angle=Math.Atan2(dx,-dy)*180/Math.PI;if(angle<0)angle+=360;int i=((int)Math.Round(angle/22.5))%16;drawRow=9+i/8;drawFrame=i%8;}}
   RenderFrame(drawRow,drawFrame);
  }
  void RenderFrame(int r,int f){if(!IsHandleCreated||IsDisposed)return;
   if(r==renderedRow&&f==renderedFrame&&Left==renderedLeft&&Top==renderedTop&&Width==renderedWidth)return;
   using(Bitmap b=new Bitmap(Width,Height,PixelFormat.Format32bppPArgb)){
    using(Graphics g=Graphics.FromImage(b)){g.Clear(Color.Transparent);g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.DrawImage(atlas,new Rectangle(0,0,Width,Height),new Rectangle(f*CW,r*CH,CW,CH),GraphicsUnit.Pixel);}
    IntPtr screen=GetDC(IntPtr.Zero),mem=CreateCompatibleDC(screen),hb=IntPtr.Zero,old=IntPtr.Zero;
    try{hb=b.GetHbitmap(Color.FromArgb(0));old=SelectObject(mem,hb);POINT pt=new POINT(Left,Top),src=new POINT(0,0);SIZE size=new SIZE(Width,Height);BLENDFUNCTION blend=new BLENDFUNCTION();blend.SourceConstantAlpha=255;blend.AlphaFormat=1;bool ok=UpdateLayeredWindow(Handle,screen,ref pt,ref size,mem,ref src,0,ref blend,2);if(!reported){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"pip-status.txt"),"LayeredWindow="+ok+"\nVisible="+IsWindowVisible(Handle)+"\nPosition="+Left+","+Top+"\nSize="+Width+"x"+Height+"\nVersion=1.0.0\n");reported=true;}if(ok){renderedRow=r;renderedFrame=f;renderedLeft=Left;renderedTop=Top;renderedWidth=Width;}}
    finally{if(old!=IntPtr.Zero)SelectObject(mem,old);if(hb!=IntPtr.Zero)DeleteObject(hb);DeleteDC(mem);ReleaseDC(IntPtr.Zero,screen);}
   }
  }
  protected override void OnFormClosed(FormClosedEventArgs e){timer.Stop();timer.Dispose();tray.Visible=false;tray.Dispose();atlas.Dispose();SystemEvents.DisplaySettingsChanged-=DisplayChanged;base.OnFormClosed(e);}
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
