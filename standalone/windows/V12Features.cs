using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PipDesktop {
 public class PipProfile {
  public string format="pip-settings";public int schemaVersion=1,size=1,speed=1,activity=1,mode=0,accessory=0,reminderMinutes=45;
  public bool homeVisible=true;public double petX=.9,petY=.9,homeX=.85,homeY=1;
  public string Json(){return new JavaScriptSerializer().Serialize(this);}
  public static PipProfile Parse(string json){
   if(json==null||json.Length>65536)throw new InvalidDataException("Слишком большой файл настроек.");
   JavaScriptSerializer s=new JavaScriptSerializer();s.MaxJsonLength=65536;
   Dictionary<string,object> raw=s.Deserialize<Dictionary<string,object>>(json);
   foreach(string key in new[]{"format","schemaVersion","size","speed","activity","mode","accessory","reminderMinutes","homeVisible","petX","petY","homeX","homeY"})if(raw==null||!raw.ContainsKey(key))throw new InvalidDataException("В файле не хватает настроек.");
   foreach(string key in new[]{"schemaVersion","size","speed","activity","mode","accessory","reminderMinutes"}){double value=Convert.ToDouble(raw[key]);if(value!=Math.Truncate(value))throw new InvalidDataException("Нужно целое значение.");}if(!(raw["homeVisible"] is bool))throw new InvalidDataException("Некорректный переключатель.");
   PipProfile p=s.Deserialize<PipProfile>(json);if(p.format!="pip-settings"||p.schemaVersion!=1||p.size<0||p.size>2||p.speed<0||p.speed>2||p.activity<0||p.activity>2||p.mode<0||p.mode>2||p.accessory<0||p.accessory>5||p.reminderMinutes<0||p.reminderMinutes>240)throw new InvalidDataException("Неподдерживаемые настройки Пипа.");
   foreach(double v in new[]{p.petX,p.petY,p.homeX,p.homeY})if(double.IsNaN(v)||double.IsInfinity(v)||v<0||v>1)throw new InvalidDataException("Некорректное положение.");return p;
  }
 }
 public class FetchGame {
  public double X,Y,VX,VY;public int Phase;public bool Visible;double age;
  public void Throw(double x,double y,double vx,double vy){X=x;Y=y;VX=Math.Max(-600,Math.Min(600,vx));VY=Math.Max(-450,Math.Min(100,vy));Phase=1;Visible=true;age=0;}
  public bool Step(double dt,ref double petX,double petWidth,double petHeight,double floor,double homeCenter,double left,double right,double pace){
   if(Phase==0)return false;age+=dt;
   if(Phase==1){X+=VX*dt;Y+=VY*dt;VY+=420*dt;VX*=Math.Pow(.55,dt);if(X<left){X=left;VX=Math.Abs(VX)*.6;}if(X>right-26){X=right-26;VX=-Math.Abs(VX)*.6;}if(Y>floor-26){Y=floor-26;VY=-Math.Abs(VY)*.42;if(Math.Abs(VY)<25)VY=0;}if(age>18){VX=0;Y=floor-26;}Move(ref petX,X+13-petWidth/2,pace*dt);petX=Math.Max(left,Math.Min(right-petWidth,petX));if(Math.Abs(petX+petWidth/2-X-13)<petWidth*.55&&Y>floor-65)Phase=2;}
   if(Phase==2){Move(ref petX,homeCenter-petWidth/2,pace*dt);X=petX+petWidth*.60;Y=floor-petHeight*.45;if(Math.Abs(petX+petWidth/2-homeCenter)<4){Phase=0;X=homeCenter+25;Y=floor-26;return true;}}
   return false;
  }
  public static void Move(ref double value,double target,double max){value+=Math.Max(-max,Math.Min(max,target-value));}
 }
 class DecorForm:Form {
  public Action<Graphics> Draw;public DecorForm(int width,int height,string title){Text=title;Width=width;Height=height;FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;TopMost=true;}
  protected override bool ShowWithoutActivation{get{return true;}}
  protected override CreateParams CreateParams{get{CreateParams p=base.CreateParams;p.ExStyle|=0x80000|0x8000000|0x80;return p;}}
  public void PaintNow(){if(!IsHandleCreated||IsDisposed)return;using(Bitmap b=new Bitmap(Width,Height,PixelFormat.Format32bppPArgb)){using(Graphics g=Graphics.FromImage(b)){g.SmoothingMode=SmoothingMode.AntiAlias;if(Draw!=null)Draw(g);}PetForm.PaintLayer(this,b);}}
  protected override void OnShown(EventArgs e){base.OnShown(e);PaintNow();}
 }
 public partial class PetForm {
  int companionMode=0,accessory=0,reminderMinutes=45,homeX=-1,homeY=-1;bool homeVisible=true,goingHome=false,sessionLocked=false,hasTray;
  DecorForm house,ball;FetchGame fetch=new FetchGame();DateTime reminderAt=DateTime.UtcNow.AddMinutes(45),lastCompany=DateTime.MinValue;Point oldCursor;
  double[,] anchorX=new double[11,8],anchorY=new double[11,8];Point homeMouse,homeOrigin,ballMouse,ballOrigin,ballLast;DateTime ballLastTime;double throwVX,throwVY;bool draggingBall,draggingHome;
  static int Nearest(int value,int[] values){int best=0;for(int i=1;i<values.Length;i++)if(Math.Abs(value-values[i])<Math.Abs(value-values[best]))best=i;return best;}
  void LoadV12(){
   using(RegistryKey k=Registry.CurrentUser.OpenSubKey(settingsSubkey)){if(k!=null){companionMode=Math.Max(0,Math.Min(2,Convert.ToInt32(k.GetValue("Mode",0))));accessory=Math.Max(0,Math.Min(5,Convert.ToInt32(k.GetValue("Accessory",0))));reminderMinutes=Math.Max(0,Math.Min(240,Convert.ToInt32(k.GetValue("ReminderMinutes",45))));homeVisible=Convert.ToInt32(k.GetValue("HomeVisible",1))==1;homeX=Convert.ToInt32(k.GetValue("HomeX",-1));homeY=Convert.ToInt32(k.GetValue("HomeY",-1));}}
   Rectangle screen=Screen.PrimaryScreen.WorkingArea;if(homeX<0){homeX=screen.Right-110;homeY=screen.Bottom-76;}ClampHome();reminderAt=DateTime.UtcNow.AddMinutes(Math.Max(1,reminderMinutes));
   using(Stream stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("pip.anchors"))using(StreamReader reader=new StreamReader(stream)){Dictionary<string,object> data=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(reader.ReadToEnd());foreach(object item in (System.Collections.IEnumerable)data["poses"]){Dictionary<string,object> p=(Dictionary<string,object>)item;int r=Convert.ToInt32(p["row"]),f=Convert.ToInt32(p["frame"]);anchorX[r,f]=Convert.ToDouble(p["x"]);anchorY[r,f]=Convert.ToDouble(p["y"]);}}
  }
  void SaveV12(RegistryKey k){k.SetValue("Mode",companionMode);k.SetValue("Accessory",accessory);k.SetValue("ReminderMinutes",reminderMinutes);k.SetValue("HomeVisible",homeVisible?1:0);k.SetValue("HomeX",homeX);k.SetValue("HomeY",homeY);}
  void InitV12(bool showTray){
   hasTray=showTray;LoadV12();ToolStripMenuItem toys=new ToolStripMenuItem("Домик и мячик");toys.DropDownItems.Add("Бросить мячик",null,delegate{ThrowBall();});toys.DropDownItems.Add("Домой",null,delegate{Touch();goingHome=true;});toys.DropDownItems.Add("Показать / убрать домик",null,delegate{homeVisible=!homeVisible;SaveSettings();SyncDecor(!Visible);});toys.DropDownItems.Add("Убрать мячик",null,delegate{fetch.Phase=0;fetch.Visible=false;SyncDecor(!Visible);});menu.Items.Insert(0,toys);
   ToolStripMenuItem company=new ToolStripMenuItem("Режим компании");string[] modes={"Свободно гулять","Рядом с курсором","У домика / в уголке"};for(int i=0;i<3;i++){int m=i;company.DropDownItems.Add(modes[i],null,delegate{companionMode=m;Touch();SaveSettings();});}menu.Items.Insert(1,company);
   ToolStripMenuItem clothes=new ToolStripMenuItem("Аксессуары");string[] names={"Без аксессуара","Шарфик","Шапочка","Красный ошейник","Синий ошейник","Зелёный ошейник"};for(int i=0;i<names.Length;i++){int a=i;clothes.DropDownItems.Add(names[i],null,delegate{accessory=a;renderedRow=-1;Touch();SaveSettings();});}menu.Items.Insert(2,clothes);
   ToolStripMenuItem reminders=new ToolStripMenuItem("Напоминания о перерыве");foreach(int minutes in new[]{0,20,30,45,60}){int m=minutes;reminders.DropDownItems.Add(m==0?"Выключены":m+" минут",null,delegate{reminderMinutes=m;reminderAt=DateTime.UtcNow.AddMinutes(Math.Max(1,m));SaveSettings();});}reminders.DropDownItems.Add("Проверить напоминание",null,delegate{Remind();});menu.Items.Insert(3,reminders);
   menu.Items.Add("Экспорт настроек…",null,delegate{ExportSettings();});menu.Items.Add("Импорт настроек…",null,delegate{ImportSettings();});
   menu.Opening+=delegate{for(int i=0;i<3;i++)((ToolStripMenuItem)company.DropDownItems[i]).Checked=i==companionMode;for(int i=0;i<names.Length;i++)((ToolStripMenuItem)clothes.DropDownItems[i]).Checked=i==accessory;};
   if(showTray){CreateDecor();Microsoft.Win32.SystemEvents.SessionSwitch+=SessionChanged;}
  }
  void SessionChanged(object sender,Microsoft.Win32.SessionSwitchEventArgs e){sessionLocked=e.Reason==Microsoft.Win32.SessionSwitchReason.SessionLock;}
  void ClampHome(){Rectangle r=Screen.FromPoint(new Point(homeX+44,homeY+36)).WorkingArea;homeX=Math.Max(r.Left,Math.Min(r.Right-88,homeX));homeY=Math.Max(r.Top,Math.Min(r.Bottom-76,homeY));}
  void CreateDecor(){
   house=new DecorForm(88,76,"Домик Пипа");house.Location=new Point(homeX,homeY);house.Draw=DrawHouse;
   house.MouseDown+=delegate(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Left){draggingHome=true;homeMouse=Cursor.Position;homeOrigin=house.Location;house.Capture=true;}};
   house.MouseMove+=delegate{if(draggingHome){Point p=Cursor.Position;house.Location=new Point(homeOrigin.X+p.X-homeMouse.X,homeOrigin.Y+p.Y-homeMouse.Y);homeX=house.Left;homeY=house.Top;house.PaintNow();}};
   house.MouseUp+=delegate{if(!draggingHome)return;draggingHome=false;house.Capture=false;ClampHome();house.Location=new Point(homeX,homeY);house.PaintNow();goingHome=true;Touch();SaveSettings();};
   house.ContextMenuStrip=new ContextMenuStrip();house.ContextMenuStrip.Items.Add("Убрать домик",null,delegate{homeVisible=false;SaveSettings();house.Hide();});
   ball=new DecorForm(28,28,"Мячик Пипа");ball.Draw=DrawBall;
   ball.MouseDown+=delegate(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Left){draggingBall=true;fetch.Phase=0;ballMouse=Cursor.Position;ballOrigin=ball.Location;ballLast=ballMouse;ballLastTime=DateTime.UtcNow;throwVX=0;throwVY=-160;ball.Capture=true;Touch();}};
   ball.MouseMove+=delegate{if(!draggingBall)return;Point p=Cursor.Position;double dt=Math.Max(.01,(DateTime.UtcNow-ballLastTime).TotalSeconds);throwVX=(p.X-ballLast.X)/dt;throwVY=(p.Y-ballLast.Y)/dt;ball.Location=new Point(ballOrigin.X+p.X-ballMouse.X,ballOrigin.Y+p.Y-ballMouse.Y);fetch.X=ball.Left;fetch.Y=ball.Top;ballLast=p;ballLastTime=DateTime.UtcNow;ball.PaintNow();};
   ball.MouseUp+=delegate{if(!draggingBall)return;draggingBall=false;ball.Capture=false;fetch.Throw(ball.Left,ball.Top,Math.Abs(throwVX)<10?180:throwVX,Math.Min(-80,throwVY));Touch();};
   ball.ContextMenuStrip=new ContextMenuStrip();ball.ContextMenuStrip.Items.Add("Убрать мячик",null,delegate{fetch.Phase=0;fetch.Visible=false;ball.Hide();});
  }
  void DrawHouse(Graphics g){using(SolidBrush wall=new SolidBrush(Color.FromArgb(255,245,218)))g.FillRectangle(wall,12,28,64,44);using(SolidBrush roof=new SolidBrush(Color.Teal))g.FillPolygon(roof,new[]{new Point(5,31),new Point(44,5),new Point(83,31)});using(SolidBrush door=new SolidBrush(Color.FromArgb(117,78,56))){g.FillPie(door,33,41,22,28,180,180);g.FillRectangle(door,33,55,22,17);}using(Pen edge=new Pen(Color.FromArgb(36,110,109),2))g.DrawLine(edge,12,72,76,72);}
  void DrawBall(Graphics g){g.FillEllipse(Brushes.Orange,2,2,24,24);using(Pen stripe=new Pen(Color.Teal,5))g.DrawArc(stripe,4,5,20,17,25,220);g.FillEllipse(Brushes.White,7,6,5,4);}
  void DrawAccessory(Graphics g,int r,int f){if(accessory==0)return;float x=(float)anchorX[r,f],ey=(float)anchorY[r,f];GraphicsState state=g.Save();g.ScaleTransform(Width/192f,Height/208f);if(accessory==2){float y=Math.Max(25,ey-24);using(SolidBrush hat=new SolidBrush(Color.MediumPurple)){g.FillPie(hat,x-17,y-20,34,32,180,180);g.FillRectangle(hat,x-21,y-5,42,8);}g.FillEllipse(Brushes.Lavender,x-3,y-23,6,6);}else{x=r==1?x-16:r==2?x+16:r>=9?100+(x-100)*.35f:x;float y=Math.Min(188,ey+32);Color color=accessory==1?Color.IndianRed:accessory==3?Color.Crimson:accessory==4?Color.RoyalBlue:Color.SeaGreen;using(Pen pen=new Pen(color,accessory==1?7:5)){pen.StartCap=pen.EndCap=LineCap.Round;g.DrawLine(pen,x-15,y,x+15,y);}using(SolidBrush brush=new SolidBrush(color)){if(accessory==1)g.FillPolygon(brush,new[]{new PointF(x+5,y),new PointF(x+20,y+22),new PointF(x+9,y+21),new PointF(x-1,y+4)});else g.FillEllipse(Brushes.Gold,x-3,y+3,6,7);}}g.Restore(state);}
  void ThrowBall(){Touch();happyUntil=DateTime.MinValue;playUntil=DateTime.MinValue;fetch.Throw(Left+Width/2,Top+Height/2,random.Next(2)==0?-220:220,-180);if(ball!=null){ball.Location=new Point((int)fetch.X,(int)fetch.Y);ball.Show();ball.PaintNow();}}
  void SyncDecor(bool hidden){if(house==null)return;house.TopMost=TopMost;ball.TopMost=TopMost;if(!hidden&&homeVisible){if(!house.Visible)house.Show();}else house.Hide();if(!hidden&&fetch.Visible){if(!ball.Visible)ball.Show();}else ball.Hide();}
  bool UpdateV12(int dt,DateTime now){
   SyncDecor(false);if(reminderMinutes>0&&now>=reminderAt){Remind();reminderAt=now.AddMinutes(reminderMinutes);}
   Rectangle area=Screen.FromPoint(new Point(homeX+44,homeY+38)).WorkingArea;double floor=homeY+76,homeCenter=Math.Max(area.Left+Width/2,Math.Min(area.Right-Width/2,homeX-Width*.2)),px=Left;bool navigating=false;
   if(fetch.Phase>0&&!draggingBall){bool arrived=fetch.Step(dt/1000.0,ref px,Width,Height,floor,homeCenter,area.Left,area.Right,speed*2.4+30);direction=px>=Left?1:-1;Left=(int)Math.Round(px);Top=(int)floor-Height;SetWalkFrame(dt);if(arrived){happyUntil=now.AddSeconds(4);SetState(6,4000);}navigating=true;}
   if(fetch.Visible&&ball!=null&&!draggingBall){ball.Location=new Point((int)fetch.X,(int)fetch.Y);ball.PaintNow();}
   if(!navigating&&!goingHome&&(now<happyUntil||now<playUntil))return false;
   if(!navigating&&(goingHome||(homeVisible&&ShouldSleep((now-touched).TotalSeconds))||companionMode==2)){MoveTo(homeCenter-Width/2,floor-Height,dt);navigating=true;if(Math.Abs(Left+Width/2-homeCenter)<4&&Math.Abs(Top+Height-floor)<4){goingHome=false;if(ShouldSleep((now-touched).TotalSeconds))RenderFrame(0,3);else RenderFrame(6,0);}}
   else if(!navigating&&companionMode==1){Point cursor=Cursor.Position;double tx=cursor.X+55,ty=cursor.Y+35;Rectangle screen=Screen.FromPoint(cursor).WorkingArea;tx=Math.Max(screen.Left,Math.Min(screen.Right-Width,tx));ty=Math.Max(screen.Top,Math.Min(screen.Bottom-Height,ty));MoveTo(tx,ty,dt);navigating=true;if(Math.Abs(Left-tx)<5&&Math.Abs(Top-ty)<5)RenderFrame(ShouldSleep((now-touched).TotalSeconds)?0:6,ShouldSleep((now-touched).TotalSeconds)?3:0);oldCursor=cursor;}
   return navigating;
  }
  void MoveTo(double x,double y,int dt){if(Math.Abs(Left-x)<4&&Math.Abs(Top-y)<4)return;double px=Left,py=Top;FetchGame.Move(ref px,x,(speed+35)*dt/1000.0);FetchGame.Move(ref py,y,(speed+35)*dt/1000.0);direction=px>=Left?1:-1;Left=(int)Math.Round(px);Top=(int)Math.Round(py);SetWalkFrame(dt);}
  void SetWalkFrame(int dt){row=direction>0?1:2;frameAge+=dt;while(frameAge>=120){frameAge-=120;frame=(frame+1)%8;}frame%=8;RenderFrame(row,frame);}
  void Remind(){if(hasTray)tray.ShowBalloonTip(5000,"Перерыв с Пипом 🦊","Может, пора встать, размяться и отдохнуть от экрана?",ToolTipIcon.Info);}
  PipProfile CurrentProfile(){Rectangle petArea=Screen.FromControl(this).WorkingArea,homeArea=Screen.FromPoint(new Point(homeX+44,homeY+38)).WorkingArea;return new PipProfile{size=Nearest(scale,new[]{112,144,192}),speed=Nearest(speed,new[]{25,55,100}),activity=activity,mode=companionMode,accessory=accessory,reminderMinutes=reminderMinutes,homeVisible=homeVisible,petX=Fraction(Left-petArea.Left,petArea.Width-Width),petY=Fraction(Top-petArea.Top,petArea.Height-Height),homeX=Fraction(homeX-homeArea.Left,homeArea.Width-88),homeY=Fraction(homeY-homeArea.Top,homeArea.Height-76)};}
  static double Fraction(double n,double d){return Math.Max(0,Math.Min(1,n/Math.Max(1,d)));}
  void ApplyProfile(PipProfile p){scale=new[]{112,144,192}[p.size];speed=new[]{25,55,100}[p.speed];activity=p.activity;companionMode=p.mode;accessory=p.accessory;reminderMinutes=p.reminderMinutes;homeVisible=p.homeVisible;Rectangle a=Screen.PrimaryScreen.WorkingArea;Width=scale;Height=scale*208/192;Left=a.Left+(int)Math.Round(p.petX*(a.Width-Width));Top=a.Top+(int)Math.Round(p.petY*(a.Height-Height));homeX=a.Left+(int)Math.Round(p.homeX*(a.Width-88));homeY=a.Top+(int)Math.Round(p.homeY*(a.Height-76));Clamp();ClampHome();if(house!=null)house.Location=new Point(homeX,homeY);reminderAt=DateTime.UtcNow.AddMinutes(Math.Max(1,reminderMinutes));renderedRow=-1;fetch.Phase=0;fetch.Visible=false;goingHome=false;Touch();SaveSettings();}
  void ExportSettings(){using(SaveFileDialog d=new SaveFileDialog()){d.Filter="Настройки Пипа (*.pip.json)|*.pip.json";d.FileName="pip-settings.pip.json";if(d.ShowDialog()==DialogResult.OK)try{File.WriteAllText(d.FileName,CurrentProfile().Json());}catch(Exception){MessageBox.Show("Не удалось сохранить настройки.","Пип");}}}
  void ImportSettings(){using(OpenFileDialog d=new OpenFileDialog()){d.Filter="Настройки Пипа (*.json)|*.json";if(d.ShowDialog()==DialogResult.OK)try{if(new FileInfo(d.FileName).Length>65536)throw new InvalidDataException();ApplyProfile(PipProfile.Parse(File.ReadAllText(d.FileName)));MessageBox.Show("Настройки перенесены. Автозапуск и системные разрешения остались прежними.","Пип");}catch(Exception){MessageBox.Show("Файл не подходит. Нужны настройки Пипа в формате .pip.json.","Пип");}}}
  void CleanupV12(){if(house!=null)house.Dispose();if(ball!=null)ball.Dispose();if(hasTray)Microsoft.Win32.SystemEvents.SessionSwitch-=SessionChanged;}
  internal static void PaintLayer(Form form,Bitmap bitmap){IntPtr screen=GetDC(IntPtr.Zero),mem=CreateCompatibleDC(screen),hb=IntPtr.Zero,old=IntPtr.Zero;try{hb=bitmap.GetHbitmap(Color.FromArgb(0));old=SelectObject(mem,hb);POINT pt=new POINT(form.Left,form.Top),src=new POINT(0,0);SIZE size=new SIZE(form.Width,form.Height);BLENDFUNCTION blend=new BLENDFUNCTION();blend.SourceConstantAlpha=255;blend.AlphaFormat=1;UpdateLayeredWindow(form.Handle,screen,ref pt,ref size,mem,ref src,0,ref blend,2);}finally{if(old!=IntPtr.Zero)SelectObject(mem,old);if(hb!=IntPtr.Zero)DeleteObject(hb);DeleteDC(mem);ReleaseDC(IntPtr.Zero,screen);}}
 }
}
