using System;
using UnityEngine;

namespace BeltSlot.Helpers
{
    // attached at runtime to the belt SlotView in corpse-loot views. each
    // LateUpdate:
    //  1. read current anchoredPosition.y (post-VLG natural position)
    //  2. if it differs from what we last wrote, VLG re-ran -> update baseline
    //  3. write anchoredPosition.y = baseline + OffsetFn()
    //
    // this lets fine-tune offsets coexist with the spacer-based layout: the
    // spacer pushes the slot at VLG level (reflow-aware on search), then we
    // sprinkle a per-slot delta on top that VLG cant absorb directly.
    //
    // identical to LegArmor's CorpseSlotOffsetter - duplicating the file
    // instead of cross-referencing because PackNStrap's Trenchfoot-BeltSlot
    // doesn't depend on LegArmor and shouldn't.
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

            // detect VLG (or anything else) repositioning us: current y will
            // not match what we last wrote. update baseline to the new value.
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
