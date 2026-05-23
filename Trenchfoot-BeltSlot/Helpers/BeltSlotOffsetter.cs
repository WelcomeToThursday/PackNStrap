using System;
using UnityEngine;

namespace BeltSlot.Helpers
{
    // attached to corpse-loot SlotViews. each LateUpdate, applies a Y
    // offset on top of the VLG-computed natural position; detects reflow
    // (search reveal, drag/drop) by noticing when current Y diverges
    // from what we last wrote, and re-anchors to the new baseline.
    //
    // duplicated from LegArmor's CorpseSlotOffsetter rather than shared
    // because Belt deliberately doesn't depend on LegArmor.
    public class BeltSlotOffsetter : MonoBehaviour
    {
        public Func<float> OffsetFn;

        private RectTransform _rt;
        private float _lastWrittenY = float.NaN;
        private float _baselineY = float.NaN;

        private void Awake()
        {
            _rt = transform as RectTransform;
        }

        private void LateUpdate()
        {
            if (_rt == null || OffsetFn == null) return;

            var currentY = _rt.anchoredPosition.y;

            if (float.IsNaN(_lastWrittenY) || Mathf.Abs(currentY - _lastWrittenY) > 0.1f)
                _baselineY = currentY;

            var offset = OffsetFn();
            var targetY = _baselineY + offset;
            if (Mathf.Abs(targetY - currentY) > 0.1f)
            {
                _rt.anchoredPosition = new Vector2(_rt.anchoredPosition.x, targetY);
                _lastWrittenY = targetY;
            }
            else
            {
                _lastWrittenY = currentY;
            }
        }
    }
}
