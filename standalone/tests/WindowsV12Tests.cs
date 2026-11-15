using System;using System.Drawing;using System.Reflection;using Microsoft.Win32;using PipDesktop;
class Tests{
 static int count;static void Check(bool value,string label){if(!value)throw new Exception(label);count++;Console.WriteLine("PASS "+label);}
 static FieldInfo F(string name){return typeof(PetForm).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);}
 static object Call(PetForm p,string name,params object[] args){return typeof(PetForm).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,args);}
 [STAThread]static void Main(){
  Check(PetForm.LookIndex(0,-1)==0&&PetForm.LookIndex(1,0)==4&&PetForm.LookIndex(0,1)==8&&PetForm.LookIndex(-1,0)==12,"cardinal gaze");
  Check(PetForm.ShouldSleep(90)&&!PetForm.ShouldSleep(89),"sleep threshold");Check(PetForm.Newer("9.0.0")&&!PetForm.Newer(PetForm.AppVersion),"version comparison");
  Check(PetForm.CoversScreen(new Rectangle(-1920,0,1920,1080),new Rectangle(-1920,0,1920,1080)),"secondary screen fullscreen");
  PipProfile profile=new PipProfile{accessory=2,mode=1,size=2,speed=0,activity=2,reminderMinutes=30,petX=.3,petY=.7,homeX=.8,homeY=1};PipProfile parsed=PipProfile.Parse(profile.Json());Check(parsed.accessory==2&&parsed.petX==.3&&parsed.mode==1,"portable settings roundtrip");
  bool rejected=false;try{PipProfile.Parse("{\"format\":\"pip-settings\"}");}catch{rejected=true;}Check(rejected,"incomplete import rejected");rejected=false;try{PipProfile bad=new PipProfile{accessory=90};PipProfile.Parse(bad.Json());}catch{rejected=true;}Check(rejected,"invalid accessory rejected");
  for(int scenario=0;scenario<3;scenario++){FetchGame g=new FetchGame();double pet=scenario==0?0:800;g.Throw(scenario==0?800:100,400,scenario==2?-550:250,-240);bool arrived=false;for(int i=0;i<4000;i++){arrived|=g.Step(.04,ref pet,144,156,800,600,0,1000,170);if(g.Phase==0)break;}Check(arrived&&g.Phase==0&&Math.Abs(pet+72-600)<4,"fetch returns home "+scenario);Check(g.Visible&&g.Y==774,"ball rests beside home "+scenario);}
  string key=@"Software\PipDesktop.Tests\"+Guid.NewGuid().ToString("N");try{
   using(PetForm p=new PetForm(key,false)){Call(p,"ApplyProfile",profile);Call(p,"Pet");Check((int)F("row").GetValue(p)==6,"petting mood");Check((int)F("accessory").GetValue(p)==2,"accessory applied");Call(p,"SaveSettings");}
   using(PetForm p=new PetForm(key,false)){Check(p.Width==192&&(int)F("companionMode").GetValue(p)==1&&(int)F("accessory").GetValue(p)==2,"new settings persist");PipProfile exported=(PipProfile)Call(p,"CurrentProfile");Check(exported.reminderMinutes==30&&exported.speed==0,"export preserves behavior");}
  }finally{if(!key.StartsWith(@"Software\PipDesktop.Tests\"))throw new Exception("unsafe key");Registry.CurrentUser.DeleteSubKeyTree(key,false);}
  Console.WriteLine("ALL "+count+" CHECKS PASSED");
 }
}
