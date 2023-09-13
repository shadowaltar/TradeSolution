using Common;
using TradeCommon.Essentials.Instruments;

namespace TradeLogicCore.Algorithms.Screening;

public class SimpleSecurityScreeningAlgoLogic : ISecurityScreeningAlgoLogic
{
    private static readonly Dictionary<int, Security> _originalPool = new();
    private static readonly Dictionary<int, Security> _pickedPool = new();

    public bool CheckIsPicked(int securityId)
    {
        lock (_pickedPool)
        {
            return _pickedPool.ContainsKey(securityId);
        }
    }

    public void SetAndPick(IDictionary<int, Security> securityPool)
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

    public IReadOnlyDictionary<int, Security> GetPickedOnes()
    {
        lock (_pickedPool)
            return _pickedPool;
    }

    public void Repick()
    {
    }

    public IReadOnlyDictionary<int, Security> GetAll()
    {
        lock (_originalPool)
            return _originalPool;
    }

    public bool TryCheckIfChanged(out IReadOnlyDictionary<int, Security> pickedOnes)
    {
        lock (_originalPool)
        {
            pickedOnes = _originalPool;
        }
        return false;
    }
}
