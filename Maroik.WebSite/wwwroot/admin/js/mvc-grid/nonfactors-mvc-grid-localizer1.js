const _localizerNonFactors1 = {
    Equals: $('#_localizerNonFactorsEquals').val(),
    NotEquals: $('#_localizerNonFactorsNotEquals').val(),
    Contains: $('#_localizerNonFactorsContains').val(),
    StartsWith: $('#_localizerNonFactorsStartsWith').val(),
    EndsWith: $('#_localizerNonFactorsEndsWith').val(),
    LessThan: $('#_localizerNonFactorsLessThan').val(),
    GreaterThan: $('#_localizerNonFactorsGreaterThan').val(),
    LessThanOrEqual: $('#_localizerNonFactorsLessThanOrEqual').val(),
    GreaterThanOrEqual: $('#_localizerNonFactorsGreaterThanOrEqual').val(),
    EarlierThan: $('#_localizerNonFactorsEarlierThan').val(),
    LaterThan: $('#_localizerNonFactorsLaterThan').val(),
    EarlierThanOrEqual: $('#_localizerNonFactorsEarlierThanOrEqual').val(),
    LaterThanOrEqual: $('#_localizerNonFactorsLaterThanOrEqual').val(),
    And: $('#_localizerNonFactorsAnd').val(),
    Or: $('#_localizerNonFactorsOr').val()
};

MvcGrid.instances = [];
MvcGrid.lang = {
    default: {
        "equals": _localizerNonFactors1.Equals,
        "not-equals": _localizerNonFactors1.NotEquals
    },
    text: {
        "contains": _localizerNonFactors1.Contains,
        "equals": _localizerNonFactors1.Equals,
        "not-equals": _localizerNonFactors1.NotEquals,
        "starts-with": _localizerNonFactors1.StartsWith,
        "ends-with": _localizerNonFactors1.EndsWith
    },
    number: {
        "equals": _localizerNonFactors1.Equals,
        "not-equals": _localizerNonFactors1.NotEquals,
        "less-than": _localizerNonFactors1.LessThan,
        "greater-than": _localizerNonFactors1.GreaterThan,
        "less-than-or-equal": _localizerNonFactors1.LessThanOrEqual,
        "greater-than-or-equal": _localizerNonFactors1.GreaterThanOrEqual
    },
    date: {
        "equals": _localizerNonFactors1.Equals,
        "not-equals": _localizerNonFactors1.NotEquals,
        "earlier-than": _localizerNonFactors1.EarlierThan,
        "later-than": _localizerNonFactors1.LaterThan,
        "earlier-than-or-equal": _localizerNonFactors1.EarlierThanOrEqual,
        "later-than-or-equal": _localizerNonFactors1.LaterThanOrEqual
    },
    guid: {
        "equals": _localizerNonFactors1.Equals,
        "not-equals": _localizerNonFactors1.NotEquals
    },
    filter: {
        "apply": "&#10003;",
        "remove": "&#10008;"
    },
    operator: {
        "select": "",
        "and": _localizerNonFactors1.And,
        "or": _localizerNonFactors1.Or
    }
};