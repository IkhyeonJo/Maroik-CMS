/// <reference types="jquery" />
// Loading the jQuery typings first makes the overloads below the ones TypeScript tries first.
//
// The library calls the tests make that the typings answer with `any`, narrowed to `unknown` (as
// TypeScripts/global.d.ts does for the scripts): a test states the type it expects, so no value is
// silently `any`. This file has no import/export, so its declarations are global.

interface JSON {
    /** Parsed JSON is whatever the text held; the test states its type. */
    parse(text: string): unknown;
}

interface JQuery<TElement = HTMLElement> {
    /** A stored data value: the test states its type. */
    data(key: string): unknown;

    /** A checkbox's checked state. */
    prop(propertyName: "checked"): boolean;
}

/** Runs script text in the page window; what it evaluates to is not used. */
declare function eval(x: string): unknown;
