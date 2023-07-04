using System;

namespace TradeApp.Models;

public record CandlePrice(double Open, double High, double Low, double Close, double Volume, double Value, DateTime Time);
