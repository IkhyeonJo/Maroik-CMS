const _localizerNonFactors1 = {
    Equals: $('#_LocalizerNonFactorsEquals').val(),
    NotEquals: $('#_LocalizerNonFactorsNotEquals').val(),
    Contains: $('#_LocalizerNonFactorsContains').val(),
    StartsWith: $('#_LocalizerNonFactorsStartsWith').val(),
    EndsWith: $('#_LocalizerNonFactorsEndsWith').val(),
    LessThan: $('#_LocalizerNonFactorsLessThan').val(),
    GreaterThan: $('#_LocalizerNonFactorsGreaterThan').val(),
    LessThanOrEqual: $('#_LocalizerNonFactorsLessThanOrEqual').val(),
    GreaterThanOrEqual: $('#_LocalizerNonFactorsGreaterThanOrEqual').val(),
    EarlierThan: $('#_LocalizerNonFactorsEarlierThan').val(),
    LaterThan: $('#_LocalizerNonFactorsLaterThan').val(),
    EarlierThanOrEqual: $('#_LocalizerNonFactorsEarlierThanOrEqual').val(),
    LaterThanOrEqual: $('#_LocalizerNonFactorsLaterThanOrEqual').val(),
    And: $('#_LocalizerNonFactorsAnd').val(),
    Or: $('#_LocalizerNonFactorsOr').val()
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
