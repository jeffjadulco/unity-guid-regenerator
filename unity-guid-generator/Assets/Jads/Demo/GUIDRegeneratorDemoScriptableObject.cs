
using UnityEngine;

namespace Jads.Demo
{
    [CreateAssetMenu(menuName = "Assets/GUID Regenerator Demo/ScriptableObject", fileName = "GUIDRegeneratorDemoScriptableObject")]
    public class GUIDRegeneratorDemoScriptableObject : ScriptableObject
    {
        [SerializeField] [Range(0, 100)] private int demoInt;
        [SerializeField] private string demoString;
        [SerializeField] private Material demoMaterial;
        [SerializeField] private Sprite demoSprite;
        [SerializeField] private GameObject demoPrefab;
        [SerializeField] private AudioClip demoAudio;
    }
}
