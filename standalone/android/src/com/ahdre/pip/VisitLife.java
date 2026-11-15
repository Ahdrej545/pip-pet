package com.ahdre.pip;
import org.json.*;
class VisitLife {
 static boolean gift(String g){return "leaf".equals(g)||"toy".equals(g)||"card".equals(g)||"none".equals(g);}
 static boolean game(String g){return "ball".equals(g)||"chase".equals(g)||"rest".equals(g);}
 static String giftName(String g){return "leaf".equals(g)?"листик":"toy".equals(g)?"игрушка":"card".equals(g)?"открытка":"без подарка";}
 static String gameName(String g){return "ball".equals(g)?"Общий мячик":"chase".equals(g)?"Догонялки":"Отдых у домика";}
 static boolean remember(JSONObject saved,JSONObject friend,JSONObject visit,boolean receiveGift,double now)throws JSONException{
  JSONArray seen=saved.optJSONArray("arrivals");if(seen==null){seen=new JSONArray();saved.put("arrivals",seen);}String id=visit.getString("id");for(int i=0;i<seen.length();i++)if(id.equals(seen.getString(i))){visit.put("meetings",friend.optInt("meetings"));return false;}seen.put(id);if(seen.length()>256)seen.remove(0);
  int meetings=Math.min(1000000,friend.optInt("meetings")+1);friend.put("meetings",meetings).put("lastMet",now);visit.put("meetings",meetings);
  String g=visit.optString("gift","none");if(receiveGift&&visit.optInt("features")>=2&&gift(g)&&!g.equals("none")){JSONObject counts=saved.optJSONObject("giftCounts");if(counts==null){counts=new JSONObject();saved.put("giftCounts",counts);}counts.put(g,Math.min(1000000,counts.optInt(g)+1));JSONArray old=saved.optJSONArray("gifts"),arr=new JSONArray();arr.put(new JSONObject().put("visit",id).put("name",friend.optString("name","Пип")).put("kind",g).put("time",now));if(old!=null)for(int i=0;i<Math.min(39,old.length());i++)arr.put(old.getJSONObject(i));saved.put("gifts",arr);}return true;
 }
 static class Frame {
  double host=.35,guest=.65,ball,ballHeight;int hostRow=0,guestRow=0,hostFrame=3,guestFrame=3;boolean showBall;
  static Frame at(String game,double t){Frame f=new Frame();if(t<0){f.hostRow=f.guestRow=3;f.hostFrame=f.guestFrame=((int)(Math.max(0,t+3)*7))%4;return f;}if(t>=30||!VisitLife.game(game)||"rest".equals(game))return f;
   int frame=(int)(t*8)%8;if("ball".equals(game)){double a=t*Math.PI/3;f.host=.18+.20*Math.max(0,Math.sin(a));f.guest=.82-.20*Math.max(0,-Math.sin(a));f.ball=.5-.32*Math.cos(a);f.ballHeight=8+Math.abs(Math.sin(a))*35;f.showBall=true;f.hostRow=1;f.guestRow=2;}else{double a=t*1.2;f.host=.5+.30*Math.sin(a);f.guest=.5+.30*Math.sin(a-1.2);f.hostRow=Math.cos(a)>=0?1:2;f.guestRow=Math.cos(a-1.2)>=0?1:2;}f.hostFrame=f.guestFrame=frame;return f;
  }
 }
}
