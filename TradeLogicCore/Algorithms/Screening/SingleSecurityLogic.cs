using TradeCommon.Essentials.Instruments;

namespace TradeLogicCore.Algorithms.Screening;

public class SingleSecurityLogic<T> : ISecurityScreeningAlgoLogic<T> where T : IAlgorithmVariables
{
    private Security? _security;
    private readonly Dictionary<int, Security> _securities = new(1);

    public IAlgorithm<T> Algorithm { get; }

    public SingleSecurityLogic(IAlgorithm<T> algorithm, Security? security)
    {
        Algorithm = algorithm;
        _security = security;
        if (security != null)
            _securities = new Dictionary<int, Security> { { security.Id, security } };
        else
            _securities = new Dictionary<int, Security>();
    }

    public void SetAndPick(IDictionary<int, Security> securityPool)
    {
        if (securityPool == null || securityPool.Count == 0) throw new ArgumentNullException(nameof(securityPool));

        _security = securityPool.Values.First();
        if (_security == null) throw new InvalidOperationException("Must provide at least one security in the pool for screening.");

        _securities.Clear();
        _securities[_security.Id] = _security;
    }

    public bool CheckIsPicked(int securityId)
    {
        if (_security == null) return false;
        return securityId == _security?.Id;
    }

    public IReadOnlyDictionary<int, Security> GetPickedOnes()
    {
        return _securities;
    }

    public IReadOnlyDictionary<int, Security> GetAll()
    {
        return _securities;
    }

    public bool TryCheckIfChanged(out IReadOnlyDictionary<int, Security> pickedOnes)
    {
        pickedOnes = _securities;
        return false;
    }

    public void Repick()
    {
    }
}
