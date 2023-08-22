using TradeCommon.Essentials.Instruments;

namespace TradeLogicCore.Algorithms.Screening;

public class SimpleSecurityScreeningAlgoLogic : ISecurityScreeningAlgoLogic
{
    private readonly List<Security> _originalPool = new();
    private readonly Dictionary<int, Security> _pickedPool = new();

    public void SetAndPick(List<Security> securityPool)
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

    public bool CheckIsPicked(int securityId)
    {
        lock (_pickedPool)
        {
            return _pickedPool.ContainsKey(securityId);
        }
    }

    public IReadOnlyCollection<Security> GetPickedOnes()
    {
        lock (_pickedPool)
            return _pickedPool.Values;
    }

    public void Repick()
    {
    }
}

public class SingleSecurityLogic : ISecurityScreeningAlgoLogic
{
    private Security? _security;
    private readonly List<Security> _securities = new(1);

    public void SetAndPick(List<Security> securityPool)
    {
        if (securityPool == null || securityPool.Count == 0) throw new ArgumentNullException(nameof(securityPool));

        _security = securityPool[0];
        if (_security == null) throw new InvalidOperationException("Must provide at least one security in the pool for screening.");

        _securities.Clear();
        _securities.Add(_security);
    }

    public bool CheckIsPicked(int securityId)
    {
        if (_security == null) return false;
        return securityId == _security?.Id;
    }

    public IReadOnlyCollection<Security> GetPickedOnes()
    {
        return _securities;
    }

    public void Repick()
    {
    }
}
