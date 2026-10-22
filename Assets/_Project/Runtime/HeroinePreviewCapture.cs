using System;
using System.Collections;
using System.IO;
using HGame.Core;
using UnityEngine;
using UnityEngine.UI;
namespace HGame.Unity
{
    // Rendering helper used only with an explicit preview command line; no gameplay assertions.
    public sealed class HeroinePreviewCapture : MonoBehaviour
    {
        private string directory;
        private IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--verification-dir");
            directory=at>=0?args[at+1]:Path.Combine(Application.persistentDataPath,"HeroinePreview");Directory.CreateDirectory(directory);
            yield return null;var p=GetComponent<JrpgPresenter>();
            yield return Shot("01-menu");var frames=DrawFrames();yield return Shot("00-walk-frames");frames.SetActive(false);Destroy(frames);p.NewGame();yield return Shot("02-town");
            for(int facing=0;facing<4;facing++){p.World.SetGridPosition(p.World.Position.x,p.World.Position.y,facing);yield return Shot("03-facing-"+facing);}
            p.OpenWindow("Shop");yield return Shot("04-shop");p.OpenWindow("");
            p.RunCommand(new GameCommand(CommandKind.Depart));yield return Shot("05-dungeon");
            p.RunCommand(new GameCommand(CommandKind.EnterNode,"c1_n01"));yield return Shot("06-battle");
            p.Execute(new GameCommand(CommandKind.Attack,p.Session.CaptureRun().Battle.Enemies[0].InstanceId));
            yield return new WaitForSeconds(.22f);yield return Shot("07-action");
            yield return new WaitForSeconds(.6f);Application.Quit();
        }
        private IEnumerator Shot(string name)
        {yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+".png"));yield return new WaitForSeconds(.18f);}
        private GameObject DrawFrames()
        {
            var root=new GameObject("Asset preview only",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;root.GetComponent<Canvas>().sortingOrder=100;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            UiKit.PanelAt("Backdrop",root.transform,0,0,960,720,UiKit.Background);var art=new JrpgArt();
            int[] directions={2,3,1,0};string[] names={"朝下","朝左","朝右","朝上"};
            for(int row=0;row<4;row++)
            {
                UiKit.Text(names[row],root.transform,22,68+row*164,150,32,22,UiKit.Gold);
                for(int col=0;col<3;col++)
                {
                    var r=UiKit.Rect("Frame",root.transform,205+col*230,5+row*175,140,170);var image=r.gameObject.AddComponent<Image>();
                    image.sprite=art.GetHero(directions[row],col);image.preserveAspect=true;
                }
            }
            return root;
        }
    }
}
