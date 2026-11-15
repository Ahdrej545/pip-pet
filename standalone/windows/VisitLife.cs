using System;using System.Collections.Generic;
namespace PipDesktop {
 public class GiftRecord {public string visit,name,kind;public double time;}
 public static class VisitLife {
  public static bool Gift(string g){return g=="leaf"||g=="toy"||g=="card"||g=="none";}
  public static bool Game(string g){return g=="ball"||g=="chase"||g=="rest";}
  public static string GiftName(string g){return g=="leaf"?"листик":g=="toy"?"игрушка":g=="card"?"открытка":"без подарка";}
  public static string GameName(string g){return g=="ball"?"Общий мячик":g=="chase"?"Догонялки":"Отдых у домика";}
  public static bool Remember(RelaySaved saved,RelayFriend f,FriendVisit v,bool receiveGift,double now){
   if(saved.arrivals==null)saved.arrivals=new List<string>();if(saved.gifts==null)saved.gifts=new List<GiftRecord>();
   if(saved.arrivals.Contains(v.id)){v.meetings=f.meetings;return false;}saved.arrivals.Add(v.id);if(saved.arrivals.Count>256)saved.arrivals.RemoveAt(0);
   f.meetings=Math.Min(1000000,f.meetings+1);f.lastMet=now;v.meetings=f.meetings;
   if(receiveGift&&v.features>=2&&Gift(v.gift)&&v.gift!="none"){if(saved.giftCounts==null)saved.giftCounts=new Dictionary<string,int>();int count;saved.giftCounts.TryGetValue(v.gift,out count);saved.giftCounts[v.gift]=Math.Min(1000000,count+1);saved.gifts.Insert(0,new GiftRecord{visit=v.id,name=f.name,kind=v.gift,time=now});if(saved.gifts.Count>40)saved.gifts.RemoveAt(saved.gifts.Count-1);}return true;
  }
 }
 public class DuoFrame {
  public double host,guest,ball,ballHeight;public int hostRow,guestRow,hostFrame,guestFrame;public bool showBall;
  public static DuoFrame At(string game,double t){DuoFrame f=new DuoFrame{host=.35,guest=.65,hostRow=0,guestRow=0,hostFrame=3,guestFrame=3};
   if(t<0){f.hostRow=f.guestRow=3;f.hostFrame=f.guestFrame=((int)(Math.Max(0,t+3)*7))%4;return f;}
   if(t>=30||!VisitLife.Game(game)||game=="rest")return f;
   int frame=(int)(t*8)%8;
   if(game=="ball"){double a=t*Math.PI/3;f.host=.18+.20*Math.Max(0,Math.Sin(a));f.guest=.82-.20*Math.Max(0,-Math.Sin(a));f.ball=.5-.32*Math.Cos(a);f.ballHeight=8+Math.Abs(Math.Sin(a))*35;f.showBall=true;f.hostRow=1;f.guestRow=2;}
   else{double a=t*1.2;f.host=.5+.30*Math.Sin(a);f.guest=.5+.30*Math.Sin(a-1.2);f.hostRow=Math.Cos(a)>=0?1:2;f.guestRow=Math.Cos(a-1.2)>=0?1:2;}
   f.hostFrame=f.guestFrame=frame;return f;
  }
 }
}
