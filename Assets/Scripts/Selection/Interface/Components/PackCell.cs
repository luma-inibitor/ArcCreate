using System.Threading;
using ArcCreate.Selection.Select;
using ArcCreate.Selection.SoundEffect;
using ArcCreate.Storage;
using ArcCreate.Storage.Data;
using ArcCreate.Utility.InfiniteScroll;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArcCreate.Selection.Interface
{
    public class PackCell : Cell
    {
        [SerializeField] private StorageData storage;
        [SerializeField] private SelectableStorage selectable;
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text title;
        [SerializeField] private RawImage image;

        private PackStorage pack;

        public override void SetCellData(CellData cellData)
        {
            PackCellData data = cellData as PackCellData;
            pack = data.PackStorage;
            selectable.StorageUnit = pack;
            title.text = pack.PackName;

            if (storage.TryAssignTextureFromCache(image, pack, pack.ImagePath))
            {
                FitCover();
                MarkFullyLoaded();
            }
        }

        public override async UniTask LoadCellFully(CellData cellData, CancellationToken cancellationToken)
        {
            await storage.AssignTexture(image, pack, pack.ImagePath);
            FitCover();
        }

        /// <summary>
        /// Crop the pack image to the cell instead of stretching it: keep its aspect ratio, fill the cell,
        /// and cut the overflow evenly from both sides.
        /// </summary>
        private void FitCover()
        {
            Texture texture = image.texture;
            Rect rect = image.rectTransform.rect;
            if (texture == null || texture.height == 0 || rect.width <= 0 || rect.height <= 0)
            {
                // No texture, or the cell has not been laid out yet: OnRectTransformDimensionsChange
                // refits it once it has a size.
                image.uvRect = new Rect(0, 0, 1, 1);
                return;
            }

            float textureAspect = (float)texture.width / texture.height;
            float cellAspect = rect.width / rect.height;
            if (textureAspect > cellAspect)
            {
                float width = cellAspect / textureAspect;
                image.uvRect = new Rect((1 - width) / 2, 0, width, 1);
            }
            else
            {
                float height = textureAspect / cellAspect;
                image.uvRect = new Rect(0, (1 - height) / 2, 1, height);
            }
        }

        private void Awake()
        {
            button.onClick.AddListener(SelectSelf);
        }

        private void OnRectTransformDimensionsChange()
        {
            if (image != null && image.texture != null)
            {
                FitCover();
            }
        }

        private void OnDestroy()
        {
            button.onClick.RemoveListener(SelectSelf);
        }

        private void SelectSelf()
        {
            if (Services.Select.IsAnySelected || storage.IsTransitioning)
            {
                return;
            }

            storage.SelectedPack.Value = pack;
            Services.SoundEffect.Play(Sound.CellSelect);
        }
    }
}