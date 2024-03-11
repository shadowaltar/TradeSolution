using Common;
using TradeCommon.Essentials.Instruments;

namespace TradeLogicCore.Algorithms.Screening;

public class SimpleSecurityScreeningAlgoLogic : ISecurityScreeningAlgoLogic
{
    private static readonly Dictionary<long, Security> _originalPool = [];
    private static readonly Dictionary<long, Security> _pickedPool = [];

    public bool CheckIsPicked(long securityId)
    {
        lock (_pickedPool)
        {
            return _pickedPool.ContainsKey(securityId);
        }
    }

    public void SetAndPick(IDictionary<long, Security> securityPool)
    {
        lock (_originalPool)
        {
            _originalPool.Clear();
            _originalPool.AddRange(securityPool);
            lock (_pickedPool)
            {
                _pickedPool.Clear();
                foreach (var pair in _originalPool)
                {
                    _pickedPool[pair.Key] = pair.Value;
                }
            }
        }
    }

    public IReadOnlyDictionary<long, Security> GetPickedOnes()
    {
        lock (_pickedPool)
            return _pickedPool;
    }

    public void Repick()
    {
    }

    public IReadOnlyDictionary<long, Security> GetAll()
    {
        lock (_originalPool)
            return _originalPool;
    }

    public bool TryCheckIfChanged(out IReadOnlyDictionary<long, Security> pickedOnes)
    {
        lock (_originalPool)
        {
            pickedOnes = _originalPool;
        }
        return false;
    }
}
