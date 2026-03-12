using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ClothDynamics
{
    [ExecuteAlways]
    public class LoadSceneManager : MonoBehaviour
    {
        [SerializeField] private Dropdown _dd;
        private static bool _loaded = false;
        // Start is called before the first frame update
        void OnEnable()
        {
#if UNITY_EDITOR
            if (_dd != null)
            {
                _dd.options.Clear();
                _dd.options = new List<Dropdown.OptionData>();

                var clothPath = Directory.GetParent(Directory.GetParent(Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(this)))).FullName);

                string path = clothPath + "/Scenes/Examples/";
                //if (GraphicsSettings.currentRenderPipeline)
                //{
                //    if (GraphicsSettings.currentRenderPipeline.GetType().ToString().Contains("HighDefinition"))
                //    {
                //        path = clothPath + "/Scenes/HDRP/";
                //    }
                //    else // assuming here we only have HDRP or URP options here
                //    {
                //        path = clothPath + "/Scenes/URP/";
                //    }
                //}

                if (Directory.Exists(path))
                {
                    var scenes = Directory.GetFiles(path, "*.unity");
                    if (scenes != null && scenes.Length > 0)
                    {
                        scenes = scenes.Select(x => Path.GetFileNameWithoutExtension(x)).ToArray();
                        for (int i = 0; i < scenes.Length; i++)
                        {
                            Dropdown.OptionData data = new Dropdown.OptionData(scenes[i]);
                            _dd.options.Add(data);
                        }
                    }
                }
            }
#endif
            if (Application.isPlaying)
            {
                if (_loaded) { this.gameObject.SetActive(false); return; }
                _loaded = true;
                DontDestroyOnLoad(this.gameObject);
            }
        }

        public void LoadScene(Dropdown dd)
        {
            SceneManager.LoadSceneAsync(dd.options[dd.value].text);
        }
    }
}