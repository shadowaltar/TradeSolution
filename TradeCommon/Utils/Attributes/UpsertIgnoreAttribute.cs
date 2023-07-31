using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeCommon.Utils.Attributes;
public class UpsertIgnoreAttribute : Attribute
{
}

public class UpsertConflictKeyAttribute : Attribute
{
}

public class UpsertConverterAttribute<TIn, TOut> : Attribute
{
    //public UpsertConverterAttribute(Func<TIn, TOut> toDatabaseFunc)
    //{
    //    ToDatabaseFunc = toDatabaseFunc;
    //}

    public Func<TIn, TOut>? ToDatabaseFunc { get; set; }
}
