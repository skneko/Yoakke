// Copyright (c) 2021 Yoakke.
// Licensed under the Apache License, Version 2.0.
// Source repository: https://github.com/LanguageDev/Yoakke

using Newtonsoft.Json;

namespace Yoakke.Lsp.Model.Capabilities.Client.TextDocument
{
    /// <summary>
    /// See https://microsoft.github.io/language-server-protocol/specifications/specification-current/#declarationClientCapabilities.
    /// </summary>
    public class DeclarationClientCapabilities
    {
        /// <summary>
        /// Whether declaration supports dynamic registration. If this is set to
        /// `true` the client supports the new `DeclarationRegistrationOptions`
        /// return value for the corresponding server capability as well.
        /// </summary>
        [JsonProperty("dynamicRegistration", NullValueHandling = NullValueHandling.Ignore)]
        public bool? DynamicRegistration { get; set; }

        /// <summary>
        /// The client supports additional metadata in the form of declaration links.
        /// </summary>
        [JsonProperty("linkSupport", NullValueHandling = NullValueHandling.Ignore)]
        public bool? LinkSupport { get; set; }
    }
}
