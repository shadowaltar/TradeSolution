using TradeCommon.Essentials.Instruments;

namespace TradeLogicCore.Algorithms.Screening;

public class SimpleSecurityScreeningAlgoLogic : ISecurityScreeningAlgoLogic
{
    private static readonly List<Security> _empty = new();
    private static readonly List<Security> _originalPool = new();
    private static readonly Dictionary<int, Security> _pickedPool = new();

    public bool CheckIsPicked(int securityId)
    {
        lock (_pickedPool)
        {
            return _pickedPool.ContainsKey(securityId);
        }
    }

    public IReadOnlyCollection<Security> GetPickedOnes(List<Security> securityPool)
    {
        lock (_pickedPool)
            return _pickedPool.Values;
    }

    public void Pick(List<Security> securityPool)
    {
        lock (_originalPool)
        {
            _originalPool.Clear();
            _originalPool.AddRange(securityPool);
            lock (_pickedPool)
            {
                _pickedPool.Clear();
                foreach (var security in _originalPool)
                {
                    _pickedPool[security.Id] = security;
                }
            }
        }
    }
}

public class SingleSecurityLogic : ISecurityScreeningAlgoLogic
{
    private Security? _security;
    private List<Security> _securities;
    public SingleSecurityLogic(Security? security)
    {
        _security = security;
        if (security != null)
            _securities = new List<Security> { security };
        else
            _securities = new List<Security>();
    }

    public bool CheckIsPicked(int securityId)
    {
        if (_security == null) return false;
        return securityId == _security?.Id;
    }

    public IReadOnlyCollection<Security> GetPickedOnes(List<Security> securityPool)
    {
        return _securities;
    }

    public void Pick(List<Security> securityPool)
    {
    }
}
