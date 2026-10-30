using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CustomAchievements
{
    /// <summary>
    /// 用官方"获得成就"弹窗卡片显示解锁。卡片打开后把它的数据清掉：官方的点击处理会按 ID 在官方面板里查找，MOD 成就查不到会报错。
    /// 清掉后官方点击只会关闭卡片，跳转由 <see cref="PopupClick"/> 完成。
    /// Shows unlocks with the vanilla "Achievement unlocked" popup card. Its data is cleared after opening: the vanilla click handler looks the ID up in the vanilla panel, which fails for mod achievements.
    /// With the data cleared the vanilla click only closes the card; <see cref="PopupClick"/> does the navigation.
    /// </summary>
    internal static class UnlockPopup
    {
        static readonly FieldInfo cardsField = AccessTools.Field(typeof(UIVariousPopupGroup), "cards");
        static readonly MethodInfo refreshLayout = AccessTools.Method(typeof(UIVariousPopupGroup), "RefreshCardsLayout");
        static readonly FieldInfo iconField = AccessTools.Field(typeof(AchievementProto), "_iconSprite");
        static readonly MethodInfo dataSetter = AccessTools.PropertySetter(typeof(ManualBehaviour), "data");

        internal static bool Show(AchRecord rec)
        {
            UIRoot root = UIRoot.instance;
            if (root == null || root.uiGame == null)
            {
                return false;
            }
            UIVariousPopupGroup group = root.uiGame.variousPopupGroup;
            if (group == null || !group.inited || !(cardsField?.GetValue(group) is List<UIVariousPopupCard> cards)
                || refreshLayout == null || iconField == null || dataSetter == null)
            {
                return false;
            }

            var proto = new AchievementProto
            {
                name = rec.Name.ToString(),
                description = rec.Description.ToString(),
            };
            iconField.SetValue(proto, IconCache.Get(rec.IconRef, rec.IconPng));

            UIVariousPopupCard card = Object.Instantiate(group.achievementCardPrefab, group.achievementCardPrefab.rectTrans.parent);
            card._Create();
            card._Init(proto);
            card._Open();
            dataSetter.Invoke(card, new object[] { null });
            card.gameObject.AddComponent<PopupClick>().FullId = rec.FullId;

            cards.Insert(0, card);
            refreshLayout.Invoke(group, null);
            group.transform.SetAsLastSibling();
            group.scrollBox.verticalNormalizedPosition = 1f;
            return true;
        }
    }

    /// <summary>左键点击弹窗：打开成就面板的 MOD 页并滚动到该成就。 / Left click on the popup: open the mod page of the achievement panel and scroll to it.</summary>
    internal sealed class PopupClick : MonoBehaviour, IPointerUpHandler
    {
        internal string FullId;

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData != null && !eventData.dragging && eventData.button == PointerEventData.InputButton.Left)
            {
                PanelView.Reveal(FullId);
            }
        }
    }
}
