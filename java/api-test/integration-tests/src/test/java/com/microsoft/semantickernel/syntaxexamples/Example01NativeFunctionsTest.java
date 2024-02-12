// Copyright (c) Microsoft. All rights reserved.

package com.microsoft.semantickernel.syntaxexamples;

import com.microsoft.semantickernel.plugins.text.TextPlugin;
import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.Test;

public class Example01NativeFunctionsTest {

    @Test
    public void run() {
        // Load native plugin
        TextPlugin text = new TextPlugin();

        // Use function without kernel
        String result = text.uppercase("ciao!");

        Assertions.assertEquals("CIAO!", result);
    }
}
