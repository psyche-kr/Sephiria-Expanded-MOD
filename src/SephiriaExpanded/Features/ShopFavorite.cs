using UnityEngine;
using UnityEngine.UI;

namespace SephiriaExpanded
{
    internal sealed class ShopFavorite : MonoBehaviour
    {
        private const string HeartName = "SephiriaExpanded_ShopFavoriteHeart";

        private static readonly Vector2 HeartPivot = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 HeartSize = new Vector2(11f, 10f);
        private static readonly Vector2 HeartPosition = new Vector2(12.5f, -11.5f);

        private GameObject template;
        private float nextTemplateSearch;
        private float nextPass;

        private void LateUpdate()
        {
            if (Time.unscaledTime < nextPass)
                return;

            nextPass = Time.unscaledTime + 0.2f;
            if (UIManager.Instance == null || SaveManager.Current == null)
                return;

            UI_ShopPanel shop = UIManager.Instance.GetElement<UI_ShopPanel>();
            if (shop == null || !shop.gameObject.activeInHierarchy)
                return;

            if (!ModSettings.ShopFavoriteEnabled)
            {
                HideHearts(shop);
                return;
            }

            if (template == null && !FindTemplate())
                return;

            foreach (UI_NewInventoryIcon icon in shop.shopInventoryIconList)
            {
                if (icon == null || !icon.gameObject.activeSelf)
                    continue;

                NewItemOwnInstance item = icon.Item;
                bool favorite = item != null && item.XIdx == icon.X && item.YIdx == icon.Y &&
                    IsFavorite(item.EntityID);
                SetHeart(icon.rectTransform, icon.bgImage, favorite);
            }

            foreach (UI_ReplenishmentIcon icon in shop.replenishmentIcons)
            {
                if (icon == null || !icon.gameObject.activeSelf)
                    continue;

                bool available = icon.purchasedObject == null || !icon.purchasedObject.activeSelf;
                bool favorite = available && icon.Entity != null && IsFavorite(icon.Entity.id);
                SetHeart(icon.rectTransform, icon.bgImage, favorite);
            }
        }

        private static bool IsFavorite(int entityId)
        {
            return SaveManager.Current.GetBool("Item_Favorite_" + entityId, false);
        }

        private static void HideHearts(UI_ShopPanel shop)
        {
            foreach (UI_NewInventoryIcon icon in shop.shopInventoryIconList)
                HideHeart(icon == null ? null : icon.rectTransform);

            foreach (UI_ReplenishmentIcon icon in shop.replenishmentIcons)
                HideHeart(icon == null ? null : icon.rectTransform);
        }

        private static void HideHeart(RectTransform parent)
        {
            if (parent == null)
                return;

            Transform heart = parent.Find(HeartName);
            if (heart != null)
                heart.gameObject.SetActive(false);
        }

        private bool FindTemplate()
        {
            if (Time.unscaledTime < nextTemplateSearch)
                return false;

            nextTemplateSearch = Time.unscaledTime + 5f;

            foreach (UI_SephiriteRewardElement reward in Resources.FindObjectsOfTypeAll<UI_SephiriteRewardElement>())
            {
                if (reward != null && reward.favoriteImage != null)
                {
                    template = reward.favoriteImage;
                    return true;
                }
            }

            foreach (UI_ItemIcon icon in Resources.FindObjectsOfTypeAll<UI_ItemIcon>())
            {
                if (icon != null && icon.favoriteIcon != null)
                {
                    template = icon.favoriteIcon;
                    return true;
                }
            }

            return false;
        }

        private void SetHeart(RectTransform parent, Image slotBackground, bool visible)
        {
            if (parent == null || slotBackground == null)
                return;

            Transform existing = parent.Find(HeartName);
            if (!visible)
            {
                if (existing != null)
                    existing.gameObject.SetActive(false);
                return;
            }

            if (existing != null)
            {
                existing.gameObject.SetActive(true);
                return;
            }

            GameObject heart = Instantiate(template, parent);
            heart.name = HeartName;
            heart.SetActive(true);

            RectTransform heartRect = heart.transform as RectTransform;
            if (heartRect != null)
            {
                RectTransform backgroundRect = slotBackground.rectTransform;
                heartRect.anchorMin = backgroundRect.anchorMin;
                heartRect.anchorMax = backgroundRect.anchorMin;
                heartRect.pivot = HeartPivot;
                heartRect.sizeDelta = HeartSize;
                heartRect.anchoredPosition = backgroundRect.anchoredPosition + HeartPosition;
                heartRect.localScale = Vector3.one;
                heartRect.SetAsLastSibling();
            }

            foreach (Graphic graphic in heart.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
        }
    }
}
