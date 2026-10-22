using System.Linq;
using HGame.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace HGame.Unity
{
    public sealed partial class JrpgPresenter
    {
        private RectTransform heroActor;
        private RectTransform[] enemyActors;
        private TextMeshProUGUI effectText;
        private void RenderCombat()
        {
            var snap=ResultVisible&&battleAfter!=null?battleAfter:Session.Snapshot;
            var run=snap.Run; var battle=run.Battle;
            if(battle==null) return;
            bool deep=battle.Phase==BattlePhase.Deep;
            UiKit.Text(deep?"深度戰鬥":"普通戰鬥",content,28,18,550,40,28,deep?UiKit.Pink:UiKit.Gold);
            UiKit.Text("第 "+battle.Turn+" 回合　·　"+(run.Job==Job.Dancer?"舞者":"劍士"),content,600,25,330,30,20,UiKit.Muted).alignment=TextAlignmentOptions.Right;
            heroActor=PixelImage("BattleHero","hero_left",content,736,214,142,184).rectTransform;
            UiKit.PanelAt("HeroShadow",content,742,399,130,9,new Color(0,0,0,.25f));
            UiKit.Text("旅人",content,727,184,160,32,21,UiKit.Gold).alignment=TextAlignmentOptions.Center;
            UiKit.Text("HP "+run.Hp+" / "+run.MaxHp+"\nMP "+run.Mp+" / "+run.MaxMp+(deep?"\nH "+battle.HeroH+" / 100":""),content,736,403,208,74,17);
            enemyActors=new RectTransform[battle.Enemies.Count];
            var alive=battle.Enemies.Where(e=>deep?e.Stamina>0:e.Hp>0).ToList();
            if(!alive.Any(e=>e.InstanceId==selected)) selected=alive.FirstOrDefault()?.InstanceId??"";
            for(int i=0;i<battle.Enemies.Count;i++)
            {
                var e=battle.Enemies[i]; bool living=deep?e.Stamina>0:e.Hp>0;
                float x=60+i*210,y=222+(i%2==0?28:-15);
                var marker=UiKit.Text(selected==e.InstanceId?"▼":"",content,x,y-61,180,32,28,UiKit.Gold);marker.alignment=TextAlignmentOptions.Center;
                var avatar=PixelImage("BattleEnemy_"+e.InstanceId,Data.Node(battle.NodeId).IsBoss?"boss":"enemy",content,x+24,y,134,138);
                avatar.color=living?Color.white:new Color(.3f,.35f,.38f,.5f); enemyActors[i]=avatar.rectTransform;
                UiKit.Text(e.Name,content,x-10,y-25,218,32,18,living?UiKit.Ink:UiKit.Muted).alignment=TextAlignmentOptions.Center;
                string bar=deep?"精力 "+e.Stamina+" / "+e.InitialStamina+"　H "+e.H:"HP "+e.Hp+" / "+e.MaxHp;
                UiKit.Text(bar,content,x-5,y+142,215,27,17,deep?UiKit.Pink:UiKit.Teal).alignment=TextAlignmentOptions.Center;
                if(!deep) UiKit.Text("暈眩 "+e.StunProgress+" / "+e.StunThreshold+(e.StunTurns>0?"　★":""),content,x-5,y+171,215,25,15,UiKit.Gold).alignment=TextAlignmentOptions.Center;
                string id=e.InstanceId;
                UiKit.Button("Target_"+id,selected==id?"已選定":"選為目標",content,x+6,y+201,180,34,()=>{selected=id;Refresh();},living&&!Busy&&!ResultVisible);
            }
            effectText=UiKit.Text("",content,200,135,520,95,27,UiKit.Pink);effectText.alignment=TextAlignmentOptions.Center;
            if(Busy&&battleBefore!=null&&battleAfter!=null) effectText.text=CombatDelta();
            UiKit.PanelAt("Commands",content,18,506,924,195,UiKit.Panel);
            if(ResultVisible)
            {
                string outcome=battle.Outcome==BattleOutcome.Defeat?"戰鬥結束 · 返回城鎮":battle.Outcome==BattleOutcome.StunVictory?"勝利 · 全體暈眩":battle.Outcome==BattleOutcome.HVictory?"勝利 · 精力歸零":"勝利";
                UiKit.Text(outcome,content,40,524,850,42,29,UiKit.Gold);
                UiKit.Text(status,content,40,581,600,80,20,UiKit.Muted);
                UiKit.Button("BattleContinue","繼續旅程",content,660,568,260,64,DismissResult,!Busy,UiKit.Hex("38645F"));
            }
            else
            {
                UiKit.Text(Busy?"行動結算中……":battle.AwaitingKnockdownChoice?"HP 已歸零：請選擇是否續戰。":"選擇目標與行動",content,40,514,880,28,19,battle.AwaitingKnockdownChoice?UiKit.Pink:UiKit.Muted);
                var actions=Session.GetAvailableActions().Where(a=>a.Command.Kind!=CommandKind.Skill||a.Command.TargetId==""||a.Command.TargetId==selected).ToList();
                if(Busy) foreach(var a in actions) a.Enabled=false;
                DrawActions(actions,content,40,550,880,132,430,52);
            }
        }
        private string CombatDelta()
        {
            if(battleBefore==null||battleAfter==null) return "";
            var a=battleBefore.Run;var b=battleAfter.Run;
            string text="";
            for(int i=0;i<a.Battle.Enemies.Count;i++)
            {
                var from=a.Battle.Enemies[i];var to=b.Battle.Enemies[i];
                int hp=from.Hp-to.Hp,stamina=from.Stamina-to.Stamina;
                if(hp>0) text+="−"+hp+"　";
                else if(stamina>0) text+="精力 −"+stamina+"　";
                else if(to.StunProgress>from.StunProgress||to.StunTurns>from.StunTurns) text+="暈眩累積　";
                else if(to.H!=from.H) text+="H "+(to.H-from.H>0?"+":"")+(to.H-from.H)+"　";
            }
            if(b.Hp!=a.Hp) text+="HP "+(b.Hp-a.Hp>0?"+":"")+(b.Hp-a.Hp);
            if(b.Mp!=a.Mp) text+="　MP "+(b.Mp-a.Mp>0?"+":"")+(b.Mp-a.Mp);
            if(a.Battle.Phase!=b.Battle.Phase) text="深度模式";
            return text==""?"行動完成":text;
        }
        private void AnimateCombat(float progress)
        {
            if(heroActor==null) return;
            float wave=Mathf.Sin(progress*Mathf.PI);
            heroActor.anchoredPosition=new Vector2(736-32*wave,-214);
            if(enemyActors!=null) foreach(var actor in enemyActors)
            { if(actor!=null) actor.GetComponent<Image>().color=progress>.3f&&progress<.65f?new Color(1,.6f,.6f):Color.white; }
        }
    }
}
