using System;
using UnityEngine;

namespace BeltSlot.Helpers
{
    // applies a Y offset on top of the VLG-computed natural position each
    // LateUpdate. detects real reflow (currentY diverges from lastWritten
    // by something other than -offset) and re-baselines; ignores drift
    // that exactly undoes our last offset (the VLG-pulled-us-back case)
    // to avoid compounding off-screen when the panel reflows every frame.
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
            var offset = OffsetFn();

            // current ~= lastWritten - offset means "vlg pulled us back to
            // natural" - keep existing baseline.
            var driftIsUndo = !float.IsNaN(_lastWrittenY)
                && Mathf.Abs(currentY - (_lastWrittenY - offset)) < 1f;

            if (float.IsNaN(_lastWrittenY) || (!driftIsUndo && Mathf.Abs(currentY - _lastWrittenY) > 0.1f))
                _baselineY = currentY;

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
