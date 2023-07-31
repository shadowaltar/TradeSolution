namespace TradeLogicCore.Services;

public interface IAlgorithmService
{
    bool RegisterAlgorithm<IAlgorithm>(params object[] args);

    void Run<IAlgorithm>();
}