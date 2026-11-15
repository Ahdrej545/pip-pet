using System;using System.Drawing;using System.Reflection;using Microsoft.Win32;using PipDesktop;
class Tests{
 static int checks;static void Check(bool b,string label){if(!b)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
 static FieldInfo F(string name){return typeof(PetForm).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);}
 static void Call(PetForm p,string name,params object[] args){typeof(PetForm).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,args);}
 [STAThread]static void Main(){
  Check(PetForm.LookIndex(0,-100)==0,"up gaze");Check(PetForm.LookIndex(100,0)==4,"right gaze");Check(PetForm.LookIndex(0,100)==8,"down gaze");Check(PetForm.LookIndex(-100,0)==12,"left gaze");
  Check(PetForm.ShouldSleep(90)&&!PetForm.ShouldSleep(89),"sleep boundary");Check(PetForm.Newer("9.0.0")&&!PetForm.Newer(PetForm.AppVersion)&&!PetForm.Newer("broken"),"update version comparison");
  Rectangle monitor=new Rectangle(-1920,0,1920,1080);Check(PetForm.CoversScreen(monitor,monitor),"fullscreen on secondary monitor");Check(!PetForm.CoversScreen(new Rectangle(-1920,0,1920,1040),monitor),"maximized window with taskbar is not fullscreen");
  string key=@"Software\PipDesktop.Tests\"+Guid.NewGuid().ToString("N");
  try{
   using(PetForm p=new PetForm(key,false)){
    p.Location=new Point(40,50);F("speed").SetValue(p,100);F("activity").SetValue(p,2);F("autoFullscreen").SetValue(p,false);p.TopMost=false;Call(p,"ResizePet",112);Call(p,"SaveSettings");
    Call(p,"Pet");Check((int)F("row").GetValue(p)==6&&(DateTime)F("happyUntil").GetValue(p)>DateTime.UtcNow,"petting sets happy sitting pose");
   }
   using(PetForm p=new PetForm(key,false)){Check(p.Width==112,"size survives restart");Check(p.Left==40,"position survives restart");Check((int)F("speed").GetValue(p)==100&&(int)F("activity").GetValue(p)==2,"behavior survives restart");Check(!p.TopMost&&!(bool)F("autoFullscreen").GetValue(p),"display preferences survive restart");}
  }finally{if(!key.StartsWith(@"Software\PipDesktop.Tests\"))throw new Exception("unsafe test key");Registry.CurrentUser.DeleteSubKeyTree(key,false);}
  Console.WriteLine("ALL "+checks+" CHECKS PASSED");
 }
}
