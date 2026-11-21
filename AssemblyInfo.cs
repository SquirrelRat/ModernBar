// Lets the HUD's Debug-compiled build run closer to full JIT speed.
// Standard for ExileApi source plugins (see lint jitAdvice).
[assembly: System.Diagnostics.Debuggable(System.Diagnostics.DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints)]
