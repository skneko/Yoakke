// Copyright (c) 2021 Yoakke.
// Licensed under the Apache License, Version 2.0.
// Source repository: https://github.com/LanguageDev/Yoakke

using Newtonsoft.Json;
using Yoakke.Lsp.Model.Basic;

namespace Yoakke.Lsp.Model.Capabilities.Server
{
    /// <summary>
    /// See https://microsoft.github.io/language-server-protocol/specifications/specification-current/#documentSymbolOptions.
    /// </summary>
    public class DocumentSymbolOptions : IWorkDoneProgressOptions
    {
        /// <inheritdoc/>
        [JsonProperty("workDoneProgress", NullValueHandling = NullValueHandling.Ignore)]
        public bool? WorkDoneProgress { get; set; }

        /// <summary>
        /// A human-readable string that is shown when multiple outlines trees
        /// are shown for the same document.
        /// </summary>
        [Since(3, 16, 0)]
        [JsonProperty("label", NullValueHandling = NullValueHandling.Ignore)]
        public string? Label { get; set; }
    }
}
