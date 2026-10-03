using UnityEngine.SceneManagement;

namespace Assets.Scripts.Often
{
    public class SceneMove
    {
        public static string GetActiveScene()
        {
            return SceneManager.GetActiveScene().name;
        }

        public static void LoadScene(string sceneName = "")
        {
            if (sceneName == "")
            {
                SceneManager.LoadScene(GetActiveScene());
            }
            else
            {
                SceneManager.LoadScene(sceneName.ToString());
            }
        }

        public static void LoadScene(SceneNames sceneName) => SceneManager.LoadScene(sceneName.ToString());


    }
}
