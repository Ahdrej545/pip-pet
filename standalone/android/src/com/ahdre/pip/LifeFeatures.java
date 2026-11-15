package com.ahdre.pip;
import android.app.*;import android.content.*;import android.graphics.*;import android.os.*;import android.view.*;import org.json.*;import java.text.SimpleDateFormat;import java.util.*;

class LifeFeatures {
 final PetService s;VisitTogether duo;long rewardAt=0,nextHabit=0,routineEnd=0,danceEnd=0,focusEnd=0,featherEnd=0;int routine=0,danceBpm=120,originX,previousReturn,guestFrame;boolean sleepPrepared=false,focusBreak=false,featherDrag=false;float featherAge,fx,fy;ExtraView cozy,feather,guest;WindowManager.LayoutParams cp,fp,gp;SocialClient social;
 LifeFeatures(PetService service){s=service;s.prefs.edit().putLong("focusUntil",0).putBoolean("focusBreak",false).apply();nextHabit=SystemClock.uptimeMillis()+35000;cozy=new ExtraView(0);feather=new ExtraView(1);guest=new ExtraView(2);cp=s.extra.params(s.dp(115),s.dp(52));fp=s.extra.params(s.dp(25),s.dp(30));gp=s.extra.params(s.dp(85),s.dp(106));cp.flags|=WindowManager.LayoutParams.FLAG_NOT_TOUCHABLE;gp.flags|=WindowManager.LayoutParams.FLAG_NOT_TOUCHABLE;s.wm.addView(cozy,cp);s.wm.addView(feather,fp);s.wm.addView(guest,gp);feather.setVisibility(View.INVISIBLE);guest.setVisibility(View.INVISIBLE);social=new SocialClient(s);duo=new VisitTogether(this);if(s.prefs.getString("diary","").length()==0)note(s.prefs,"Обустроился рядом. Начинается наша история!");}
 static int level(SharedPreferences p){return 1+(int)Math.sqrt(p.getInt("bond",0)/5.0);}
 static synchronized void note(SharedPreferences p,String text){try{JSONArray old=new JSONArray(p.getString("diary","[]")),a=new JSONArray();a.put(new SimpleDateFormat("dd.MM HH:mm",Locale.getDefault()).format(new Date())+" · "+text);for(int i=0;i<Math.min(39,old.length());i++)a.put(old.getString(i));p.edit().putString("diary",a.toString()).apply();}catch(JSONException e){p.edit().putString("diary","[]").apply();}}
 void reward(String text){long now=SystemClock.uptimeMillis();if(now<rewardAt)return;rewardAt=now+30000;s.prefs.edit().putInt("bond",Math.min(100000,s.prefs.getInt("bond",0)+1)).apply();note(s.prefs,text);}
 void stopActivity(){routineEnd=danceEnd=focusEnd=featherEnd=0;focusBreak=false;s.extra.phase=0;s.happyUntil=0;s.playUntil=0;}
 void action(String action){long now=SystemClock.uptimeMillis();if("PET".equals(action))reward("Погладили за ушком. Хорошо рядом с тобой.");
  else if("PAW".equals(action)){stopActivity();s.touch();routine=3;routineEnd=now+1400;reward(level(s.prefs)<2?"Учились давать лапу. Пока получается приветствие!":"Дал лапу. А потом ещё раз — на всякий случай.");}
  else if("FEATHER".equals(action)){stopActivity();s.touch();featherAge=0;featherEnd=now+25000;fx=s.lp.x+s.lp.width/2;fy=s.lp.y+s.lp.height/2;note(s.prefs,"Началась охота за пёрышком.");}
  else if("FOUND".equals(action)){s.touch();s.setState(4,850);s.happyUntil=now+2000;reward("Нашли спрятанную игрушку. Коробка выдала себя!");}
  else if("DANCE".equals(action)){stopActivity();s.touch();danceBpm=s.prefs.getInt("danceBpm",120);danceEnd=now+60000;reward("Потанцевали под музыку. Хвост задавал ритм.");}
  else if("STOP_DANCE".equals(action))danceEnd=0;
  else if("FOCUS".equals(action)){stopActivity();s.touch();focusEnd=SystemClock.elapsedRealtime()+s.prefs.getInt("focusMinutes",25)*60000L;note(s.prefs,"Сижу рядом, пока ты работаешь.");}
  else if("FOCUS_BREAK".equals(action)){if(focusEnd>0&&!focusBreak)focusEnd=SystemClock.elapsedRealtime();}
  else if("STOP_FOCUS".equals(action)){focusEnd=0;focusBreak=false;}
  syncFocus();
 }
 void visible(boolean show){if(cozy==null)return;cozy.setVisibility(show&&s.prefs.getBoolean("cozy",true)&&s.extra.homeVisible?View.VISIBLE:View.INVISIBLE);if(!show||SystemClock.uptimeMillis()>=featherEnd)feather.setVisibility(View.INVISIBLE);if(!show||social.visitor()==null)guest.setVisibility(View.INVISIBLE);}
 boolean tick(int dt,long now){social.tick();if(social.away()){s.pet.setVisibility(View.INVISIBLE);s.extra.visible(false);visible(false);return true;}visible(true);
  int x=Math.max(0,Math.min(s.width-cp.width,s.extra.hp.x-s.dp(58))),y=Math.min(s.height-cp.height-s.dp(28),s.extra.hp.y+s.dp(5));if(cp.x!=x||cp.y!=y){cp.x=x;cp.y=y;s.wm.updateViewLayout(cozy,cp);}
  if(s.extra.returns>previousReturn){reward("Принёс мяч домой. Можно ещё один бросок?");if(s.prefs.getBoolean("habits",true)&&s.random.nextInt(3)==0){s.extra.ballVisible=false;note(s.prefs,"Убрал мячик в коробку.");}}previousReturn=s.extra.returns;
  String giftShelf=s.prefs.getString("giftCounts","{}");if(!giftShelf.equals(duo.shelf)){duo.shelf=giftShelf;cozy.invalidate();}
  if(duo.tick(now))return true;
  if(social.visitor()!=null){gp.x=Math.max(0,Math.min(s.width-gp.width,s.lp.x-gp.width+s.dp(12)));gp.y=Math.max(s.dp(24),s.lp.y+s.lp.height-gp.height);s.wm.updateViewLayout(guest,gp);guestFrame=(int)(now/180)%4;guest.setVisibility(View.VISIBLE);guest.invalidate();}
  if(focusEnd>0&&SystemClock.elapsedRealtime()>=focusEnd){if(!focusBreak){focusBreak=true;focusEnd=SystemClock.elapsedRealtime()+300000;note(s.prefs,"Работа позади. Пять минут на разминку и мячик!");s.extra.remind();s.extra.action("BALL");}else{focusEnd=0;focusBreak=false;note(s.prefs,"Перерыв завершён. Рабочий режим можно запустить снова.");}}
  syncFocus();
  if(focusEnd>0&&!focusBreak){pose(6,0);return true;}
  if(now<featherEnd){featherAge+=dt/1000f;if(!featherDrag){fx=Math.max(s.dp(15),Math.min(s.width-s.dp(15),s.lp.x+s.lp.width/2+(float)Math.sin(featherAge*1.5)*s.dp(65)));fy=s.lp.y+s.lp.height/2-s.dp(15)+(float)Math.cos(featherAge*2)*s.dp(18);fp.x=Math.round(fx)-fp.width/2;fp.y=Math.round(fy)-fp.height/2;s.wm.updateViewLayout(feather,fp);}feather.setVisibility(View.VISIBLE);feather.invalidate();s.lp.x=(int)V12Features.move(s.lp.x,fx-s.lp.width/2,s.dp(s.speed+35)*dt/1000.0);s.clamp();s.update();pose(7,(int)(featherAge*8)%6);if(featherAge>6&&Math.abs(s.lp.x+s.lp.width/2-fx)<s.dp(30)){featherEnd=0;feather.setVisibility(View.INVISIBLE);reward("Поймал пёрышко. Оно щекочется!");s.happyUntil=now+2000;s.setState(4,850);}return true;}
  if(now<danceEnd){int beat=(int)(now/1000.0*danceBpm/60)%4;pose(level(s.prefs)>=3&&beat==3?4:beat<2?3:6,beat%2);return true;}
  if(now<routineEnd){if(routine==3){pose(3,Math.min(3,(int)((1400-(routineEnd-now))/350)));}else{float t=2-(routineEnd-now)/1000f;s.lp.x=originX+Math.round((float)Math.sin(t*Math.PI)*s.dp(8));s.clamp();s.update();pose(routine==1?2:7,(int)(t*6)%(routine==1?8:6));}return true;}
  long idle=now-s.touched;if(idle<10000)sleepPrepared=false;
  if(s.prefs.getBoolean("habits",true)&&!sleepPrepared&&idle>=84000&&idle<90000&&s.extra.phase==0){sleepPrepared=true;routine=1;originX=s.lp.x;routineEnd=now+2000;note(s.prefs,"Перед сном покрутился на коврике. Уютно.");return true;}
  if(s.prefs.getBoolean("habits",true)&&now>=nextHabit&&s.extra.phase==0&&now>=s.happyUntil&&now>=s.playUntil){nextHabit=now+45000+s.random.nextInt(35000);if(now-s.touched>=90000)return false;routine=s.random.nextInt(2)+1;originX=s.lp.x;routineEnd=now+2000;note(s.prefs,routine==1?"Покрутился на коврике. Теперь удобно.":"Выглянул из своего уголка: ты ещё здесь?");}
  return false;
 }
 void syncFocus(){if(s.prefs.getLong("focusUntil",0)!=focusEnd||s.prefs.getBoolean("focusBreak",false)!=focusBreak)s.prefs.edit().putLong("focusUntil",focusEnd).putBoolean("focusBreak",focusBreak).apply();}
 void pose(int r,int f){s.row=r;s.frame=f;s.extra.redraw();}
 void close(){s.prefs.edit().putLong("focusUntil",0).putBoolean("focusBreak",false).apply();social.close();for(View v:new View[]{cozy,feather,guest})try{s.wm.removeView(v);}catch(RuntimeException e){}}
 class ExtraView extends View {
  int kind;Paint p=new Paint(3);ExtraView(int k){super(s);kind=k;setContentDescription(k==0?"Уютный уголок Пипа":k==1?"Пёрышко — перетащи":"Пип в гостях");}
  void color(int c){p.setColor(c);p.setStyle(Paint.Style.FILL);}
  @Override protected void onDraw(Canvas c){super.onDraw(c);c.save();if(kind==0){c.scale(getWidth()/180f,getHeight()/80f);duo.drawShelf(c,p);color(Color.rgb(175,238,238));c.drawOval(2,55,127,75,p);color(Color.rgb(216,191,216));c.drawOval(20,46,80,68,p);color(Color.rgb(210,180,140));c.drawRect(138,40,173,72,p);color(Color.rgb(255,165,0));c.drawCircle(153,40,8,p);color(Color.rgb(0,128,128));p.setStrokeWidth(2);c.drawLine(163,45,171,23,p);}else if(kind==1){c.scale(getWidth()/34f,getHeight()/40f);color(Color.rgb(0,128,128));p.setStrokeWidth(2);c.drawLine(8,35,26,5,p);color(Color.rgb(147,112,219));c.drawOval(13,4,27,29,p);color(Color.WHITE);c.drawLine(15,26,25,8,p);}else{SocialClient.Pet v=social.visitor();if(v!=null){c.drawBitmap(s.atlas,new Rect(guestFrame*192,(duo.id==null?3:duo.guestRow)*208,(guestFrame+1)*192,((duo.id==null?3:duo.guestRow)+1)*208),new RectF(0,s.dp(14),getWidth(),getHeight()),p);int old=s.extra.accessory;c.save();c.translate(0,s.dp(14));try{s.extra.accessory=v.accessory;s.extra.drawAccessory(c,duo.id==null?3:duo.guestRow,guestFrame,getWidth(),getHeight()-s.dp(14));}finally{s.extra.accessory=old;c.restore();}color(Color.WHITE);c.drawRect(0,0,getWidth(),s.dp(15),p);color(Color.rgb(0,100,100));p.setTextSize(s.dp(10));c.drawText((social.activeVisit()!=null&&social.activeVisit().optInt("meetings")>1?"Снова: ":"Гость: ")+v.name,s.dp(2),s.dp(11),p);}}c.restore();}
  @Override public boolean onTouchEvent(MotionEvent e){if(kind!=1)return false;if(e.getActionMasked()==MotionEvent.ACTION_DOWN){featherDrag=true;s.touch();return true;}if(e.getActionMasked()==MotionEvent.ACTION_MOVE){fx=e.getRawX();fy=e.getRawY();fp.x=Math.round(fx)-fp.width/2;fp.y=Math.round(fy)-fp.height/2;s.wm.updateViewLayout(this,fp);return true;}if(e.getActionMasked()==MotionEvent.ACTION_UP||e.getActionMasked()==MotionEvent.ACTION_CANCEL){featherDrag=false;return true;}return true;}
 }
}
