using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace StageReport
{
    [CreateAssetMenu(fileName = "InteractableDef", menuName = "StageReport/InteractableDef", order = 1)]
    public class InteractableDef : ScriptableObject
    {
        public InteractableType type;
        public string nameToken;
        public Texture2D texture;
        public string textureKey;
        public int charges = 1;
        [Range(0, 100)]
        public int defaultScoreWeight;
        public bool unstackable;
        public string[] gameObjectNames;

        private Texture2D _texture;

        public void SetRuntimeTexture(Texture2D value)
        {
            _texture = value;
            texture = value;
            textureKey = null;
        }

        public Texture2D Texture => _texture == null
            ? _texture = (texture != null ? texture : (string.IsNullOrEmpty(textureKey) ? null : Addressables.LoadAssetAsync<Texture2D>(textureKey).WaitForCompletion()))
            : _texture;

        public int score => Application.isEditor
            ? defaultScoreWeight
            : ModConfig.interactablesScore[type].Value;
    }
}
