using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PipDesktop {
 public class LifeState {
  public int points;public bool cozy=true,habits=true;public List<string> diary=new List<string>();
  public int Level{get{return 1+(int)Math.Sqrt(points/5.0);}}
  public void Note(string text){diary.Insert(0,DateTime.Now.ToString("dd.MM HH:mm")+" · "+text);if(diary.Count>40)diary.RemoveRange(40,diary.Count-40);}
  public void Reward(string text){points=Math.Min(100000,points+1);Note(text);}
 }
 public partial class PetForm {
  LifeState life=new LifeState();DateTime rewardAt=DateTime.MinValue,nextHabit=DateTime.UtcNow.AddSeconds(35),routineEnd=DateTime.MinValue,danceEnd=DateTime.MinValue,focusEnd=DateTime.MinValue;
  int routine=0,danceBpm=120,focusMinutes=25;bool focusBreak=false;Point routineOrigin;DecorForm cozyDecor,feather;double featherAge;DateTime featherEnd=DateTime.MinValue;Point featherPoint;bool featherDrag;
  bool sleepPrepared;int previousReturn;SocialClient social;DecorForm guest;int guestFrame;DateTime guestDraw=DateTime.MinValue;bool away;
  void InitLife(){
   using(RegistryKey k=Registry.CurrentUser.OpenSubKey(settingsSubkey)){if(k!=null){try{life=new JavaScriptSerializer().Deserialize<LifeState>((string)k.GetValue("Life","{}"))??new LifeState();if(life.diary==null)life.diary=new List<string>();}catch{life=new LifeState();}}}
   if(life.diary.Count==0)life.Note("Обустроился рядом. Начинается наша история!");
   ToolStripMenuItem games=new ToolStripMenuItem("Новые игры и трюки");games.DropDownItems.Add("Поймать пёрышко",null,delegate{StartFeather();});games.DropDownItems.Add("Найти игрушку",null,delegate{FindToy();});games.DropDownItems.Add("Дай лапу",null,delegate{Paw();});menu.Items.Insert(4,games);
   ToolStripMenuItem music=new ToolStripMenuItem("Танцы под музыку");foreach(int bpm in new[]{90,120,150}){int b=bpm;music.DropDownItems.Add(b+" ударов в минуту",null,delegate{StopActivity();danceBpm=b;danceEnd=DateTime.UtcNow.AddSeconds(60);Touch();Reward("Потанцевали под музыку. Хвост задавал ритм.");});}music.DropDownItems.Add("Остановить танец",null,delegate{danceEnd=DateTime.MinValue;});menu.Items.Insert(5,music);
   ToolStripMenuItem focus=new ToolStripMenuItem("Работа и перерыв");foreach(int minutes in new[]{15,25,45}){int m=minutes;focus.DropDownItems.Add("Работать "+m+" минут, отдыхать 5",null,delegate{StartFocus(m);});}focus.DropDownItems.Add("Завершить рабочий интервал сейчас",null,delegate{if(focusEnd!=DateTime.MinValue&&!focusBreak)focusEnd=DateTime.UtcNow;});focus.DropDownItems.Add("Остановить рабочий режим",null,delegate{focusEnd=DateTime.MinValue;focusBreak=false;});menu.Items.Insert(6,focus);menu.Opening+=delegate{focus.Text=focusEnd==DateTime.MinValue?"Работа и перерыв":(focusBreak?"Перерыв · ":"Работа · ")+Math.Max(0,(int)Math.Ceiling((focusEnd-DateTime.UtcNow).TotalMinutes))+" мин осталось";};
   ToolStripMenuItem habits=new ToolStripMenuItem("Маленькие привычки");habits.Checked=life.habits;habits.CheckOnClick=true;habits.CheckedChanged+=delegate{life.habits=habits.Checked;SaveSettings();};menu.Items.Add(habits);
   ToolStripMenuItem cozy=new ToolStripMenuItem("Коврик, подушка и коробка");cozy.Checked=life.cozy;cozy.CheckOnClick=true;cozy.CheckedChanged+=delegate{life.cozy=cozy.Checked;SaveSettings();};menu.Items.Add(cozy);
   menu.Items.Add("Дружба и дневник",null,delegate{ShowDiary();});
   social=new SocialClient(this,settingsSubkey);menu.Items.Add("Друзья и визиты через интернет…",null,delegate{social.Open();});
   if(hasTray){cozyDecor=new DecorForm(180,80,"Уголок Пипа");cozyDecor.Draw=DrawCozy;cozyDecor.Enabled=false;
    feather=new DecorForm(34,40,"Пёрышко — перетащи");feather.Draw=DrawFeather;
    feather.MouseDown+=delegate{featherDrag=true;feather.Capture=true;};feather.MouseMove+=delegate{if(featherDrag){featherPoint=Cursor.Position;feather.Location=new Point(featherPoint.X-17,featherPoint.Y-20);}};feather.MouseUp+=delegate{featherDrag=false;feather.Capture=false;};
    guest=new DecorForm(128,162,"Пип в гостях");guest.Draw=DrawGuest;guest.MouseClick+=delegate{SetState(3,700);happyUntil=DateTime.UtcNow.AddSeconds(2);};
   }
  }
  void SaveLife(RegistryKey k){k.SetValue("Life",new JavaScriptSerializer().Serialize(life));}
  void Reward(string text){DateTime now=DateTime.UtcNow;if(now<rewardAt)return;rewardAt=now.AddSeconds(30);life.Reward(text);SaveSettings();}
  void StopActivity(){danceEnd=DateTime.MinValue;featherEnd=DateTime.MinValue;routineEnd=DateTime.MinValue;fetch.Phase=0;focusEnd=DateTime.MinValue;focusBreak=false;happyUntil=DateTime.MinValue;playUntil=DateTime.MinValue;}
  void Paw(){StopActivity();Touch();SetState(3,1200);happyUntil=DateTime.UtcNow.AddMilliseconds(1200);Reward(life.Level<2?"Учились давать лапу. Пока получается приветствие!":"Дал лапу. А потом ещё раз — на всякий случай.");if(life.Level>=2){routine=3;routineEnd=DateTime.UtcNow.AddMilliseconds(1400);}}
  void StartFeather(){StopActivity();Touch();featherAge=0;featherEnd=DateTime.UtcNow.AddSeconds(25);featherPoint=new Point(Left+Width/2+100,Top+Height/2);life.Note("Началась охота за пёрышком.");}
  void FindToy(){StopActivity();Touch();int hidden=random.Next(3);using(Form f=new Form()){f.Text="Где Пип спрятал игрушку?";f.Size=new Size(410,160);f.StartPosition=FormStartPosition.CenterScreen;f.FormBorderStyle=FormBorderStyle.FixedDialog;f.MaximizeBox=false;Label label=new Label{Text="Выбери коробку. Можно пробовать ещё!",Dock=DockStyle.Top,Height=35,TextAlign=ContentAlignment.MiddleCenter};f.Controls.Add(label);for(int i=0;i<3;i++){int n=i;Button b=new Button{Text="Коробка "+(i+1),Left=12+i*128,Top=50,Width=120,Height=45};b.Click+=delegate{if(n==hidden){Reward("Нашли спрятанную игрушку. Коробка выдала себя!");SetState(4,850);happyUntil=DateTime.UtcNow.AddSeconds(2);label.Text="Нашли! Пип радуется 🦊";foreach(Control c in f.Controls)if(c is Button)c.Enabled=false;}else{b.Enabled=false;label.Text="Здесь пусто. Попробуй другую коробку.";}};f.Controls.Add(b);}f.ShowDialog();}}
  void StartFocus(int minutes){StopActivity();focusMinutes=minutes;focusBreak=false;focusEnd=DateTime.UtcNow.AddMinutes(minutes);Touch();life.Note("Сижу рядом, пока ты работаешь.");SaveSettings();}
  void ShowDiary(){using(Form f=new Form()){f.Text="Дружба с Пипом · уровень "+life.Level;f.Size=new Size(540,470);f.StartPosition=FormStartPosition.CenterScreen;TextBox t=new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill,Font=new Font("Segoe UI",11),Text="Дружба: "+life.points+" тёплых моментов · уровень "+life.Level+"\r\n"+(life.Level<2?"На 2 уровне Пип увереннее даёт лапу. На 3 — танцует с прыжками.":life.Level<3?"Трюк «дай лапу» освоен. На 3 уровне — танцы с прыжками.":"Освоены лапа и танцевальные прыжки.")+"\r\nПрогресс не уменьшается, если ты занят.\r\n\r\n"+string.Join("\r\n\r\n",life.diary.ToArray())};f.Controls.Add(t);f.ShowDialog();}}
  void LifeVisible(bool visible){if(cozyDecor==null)return;cozyDecor.TopMost=TopMost;feather.TopMost=TopMost;guest.TopMost=TopMost;
   bool showCozy=visible&&life.cozy&&homeVisible;if(showCozy){Point p=new Point(Math.Max(Screen.FromControl(this).WorkingArea.Left,homeX-90),homeY+8);bool redraw=cozyDecor.Location!=p||!cozyDecor.Visible;cozyDecor.Location=p;if(!cozyDecor.Visible)cozyDecor.Show();if(redraw)cozyDecor.PaintNow();}else cozyDecor.Hide();
   if(!visible||DateTime.UtcNow>=featherEnd)feather.Hide();
   if(!visible||social==null||social.Visitor==null)guest.Hide();
  }
  bool UpdateLife(int dt,DateTime now){social.Tick(now);away=social.Away;
   if(away){if(Visible)Hide();SyncDecor(true);LifeVisible(false);return true;}LifeVisible(true);
   if(fetch.Returns>previousReturn){Reward("Принёс мяч домой. Можно ещё один бросок?");if(life.habits&&random.Next(3)==0){fetch.Visible=false;life.Note("Убрал мячик в коробку.");}}previousReturn=fetch.Returns;
   if(social.Visitor!=null&&guest!=null&&now>=guestDraw){guestDraw=now.AddMilliseconds(140);guestFrame=(guestFrame+1)%4;guest.Location=new Point(Math.Max(work.Left,Math.Min(work.Right-guest.Width,Left-guest.Width+20)),Top+Height-guest.Height);if(!guest.Visible)guest.Show();guest.PaintNow();}
   if(focusEnd!=DateTime.MinValue&&now>=focusEnd){if(!focusBreak){focusBreak=true;focusEnd=now.AddMinutes(5);life.Note("Работа позади. Пять минут на разминку и мячик!");Remind();ThrowBall();}else{focusEnd=DateTime.MinValue;focusBreak=false;life.Note("Перерыв завершён. Рабочий режим можно запустить снова.");}SaveSettings();}
   if(focusEnd!=DateTime.MinValue&&!focusBreak){RenderFrame(6,0);return true;}
   if(now<featherEnd){featherAge+=dt/1000.0;Rectangle a=Screen.FromControl(this).WorkingArea;if(!featherDrag){featherPoint=new Point((int)Math.Max(a.Left+20,Math.Min(a.Right-20,Left+Width/2+Math.Sin(featherAge*1.5)*100)),Top+Height/2-20+(int)(Math.Cos(featherAge*2)*25));feather.Location=new Point(featherPoint.X-17,featherPoint.Y-20);}if(!feather.Visible)feather.Show();feather.PaintNow();double x=Left;FetchGame.Move(ref x,featherPoint.X-Width/2,(speed+60)*dt/1000.0);Left=(int)x;Clamp();RenderFrame(7,(int)(featherAge*8)%6);if(featherAge>6&&Math.Abs(Left+Width/2-featherPoint.X)<45){featherEnd=DateTime.MinValue;feather.Hide();Reward("Поймал пёрышко. Оно щекочется!");SetState(4,850);happyUntil=now.AddSeconds(2);}return true;}
   if(now<danceEnd){double beat=now.TimeOfDay.TotalSeconds*danceBpm/60;int phase=(int)beat%4;RenderFrame(life.Level>=3&&phase==3?4:phase<2?3:6,phase%2);return true;}
   if(now<routineEnd){if(routine==3)RenderFrame(3,Math.Min(3,(int)((1.4-(routineEnd-now).TotalSeconds)*3)));else{double t=2-(routineEnd-now).TotalSeconds;Left=routineOrigin.X+(int)(Math.Sin(t*Math.PI)*12);RenderFrame(routine==1?2:7,(int)(t*6)% (routine==1?8:6));}return true;}
   double idle=(now-touched).TotalSeconds;if(idle<10)sleepPrepared=false;
   if(life.habits&&!sleepPrepared&&idle>=84&&idle<90&&fetch.Phase==0){sleepPrepared=true;routine=1;routineOrigin=Location;routineEnd=now.AddSeconds(2);life.Note("Перед сном покрутился на коврике. Уютно.");SaveSettings();return true;}
   if(life.habits&&now>=nextHabit&&fetch.Phase==0&&now>=happyUntil&&now>=playUntil){nextHabit=now.AddSeconds(45+random.Next(35));if(ShouldSleep((now-touched).TotalSeconds))return false;routine=random.Next(2)+1;routineOrigin=Location;routineEnd=now.AddSeconds(2);life.Note(routine==1?"Покрутился на коврике. Теперь удобно.":"Выглянул из своего уголка: ты ещё здесь?");SaveSettings();}
   return false;
  }
  void DrawCozy(Graphics g){g.FillEllipse(Brushes.PaleTurquoise,2,55,125,20);g.FillEllipse(Brushes.Thistle,20,46,60,22);g.FillRectangle(Brushes.Tan,138,40,35,32);g.DrawRectangle(Pens.Sienna,138,40,35,32);g.FillEllipse(Brushes.Orange,145,32,16,16);g.DrawLine(Pens.Teal,163,45,171,23);}
  void DrawFeather(Graphics g){using(Pen stem=new Pen(Color.Teal,2)){g.DrawLine(stem,8,35,26,5);}using(SolidBrush p=new SolidBrush(Color.MediumPurple)){g.FillEllipse(p,13,4,14,25);}g.DrawLine(Pens.White,15,26,25,8);}
  void DrawGuest(Graphics g){FriendPet p=social.Visitor;if(p==null)return;g.DrawImage(atlas,new Rectangle(0,16,128,139),new Rectangle(guestFrame*192,3*208,192,208),GraphicsUnit.Pixel);GraphicsState state=g.Save();g.TranslateTransform(0,16);g.ScaleTransform(128f/Width,139f/Height);int old=accessory;try{accessory=p.accessory;DrawAccessory(g,3,guestFrame);}finally{accessory=old;g.Restore(state);}using(Font font=new Font("Segoe UI",9,FontStyle.Bold)){g.FillRectangle(Brushes.WhiteSmoke,0,0,128,18);g.DrawString("Гость: "+p.name,font,Brushes.Teal,new RectangleF(2,0,124,18));}}
  void CleanupLife(){if(social!=null)social.Dispose();foreach(DecorForm f in new[]{cozyDecor,feather,guest})if(f!=null)f.Dispose();}
 }
}
