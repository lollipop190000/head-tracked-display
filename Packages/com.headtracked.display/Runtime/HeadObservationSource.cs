using UnityEngine;

namespace HeadTracked.Display
{
    public abstract class HeadObservationSource : MonoBehaviour
    {
        public abstract bool TryGetLatest(out HeadObservation observation);
        public abstract string Status { get; }
    }
}
