﻿/*
 * Copyright (C) 2023 Crypter File Transfer
 * 
 * This file is part of the Crypter file transfer project.
 * 
 * Crypter is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * The Crypter source code is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 * 
 * You should have received a copy of the GNU Affero General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * 
 * You can be released from the requirements of the aforementioned license
 * by purchasing a commercial license. Buying such a license is mandatory
 * as soon as you develop commercial activities involving the Crypter source
 * code without disclosing the source code of your own applications.
 * 
 * Contact the current copyright holder to discuss commercial license options.
 */

using Crypter.Common.Client.Interfaces;
using Crypter.Common.Contracts.Features.UserAuthentication;
using Crypter.Test.Integration_Tests.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;
using System.Threading.Tasks;

namespace Crypter.Test.Integration_Tests.UserAuthentication_Tests
{
   [TestFixture]
   internal class Registration_Tests
   {
      private Setup _setup;
      private WebApplicationFactory<Program> _factory;
      private ICrypterApiClient _client;

      [OneTimeSetUp]
      public async Task OneTimeSetUp()
      {
         _setup = new Setup();
         await _setup.InitializeRespawnerAsync();

         _factory = await Setup.SetupWebApplicationFactoryAsync();
         (_client, _) = Setup.SetupCrypterApiClient(_factory.CreateClient());
      }

      [TearDown]
      public async Task TearDown()
      {
         await _setup.ResetServerDataAsync();
      }

      [OneTimeTearDown]
      public async Task OneTimeTearDown()
      {
         await _factory.DisposeAsync();
      }

      [TestCase(TestData.DefaultEmailAdress)]
      [TestCase(null)]
      public async Task Register_User_Works(string emailAddress)
      {
         RegistrationRequest request = TestData.GetRegistrationRequest(TestData.DefaultUsername, TestData.DefaultPassword, emailAddress);
         var result = await _client.UserAuthentication.RegisterAsync(request);

         Assert.True(result.IsRight);
      }

      [TestCase("FOO", "foo")]
      [TestCase("foo", "FOO")]
      public async Task Register_User_Fails_For_Duplicate_Username(string initialUsername, string duplicateUsername)
      {
         VersionedPassword password = new VersionedPassword("password"u8.ToArray(), 1);

         RegistrationRequest initialRequest = new RegistrationRequest(initialUsername, password, null);
         var initialResult = await _client.UserAuthentication.RegisterAsync(initialRequest);

         RegistrationRequest secondRequest = new RegistrationRequest(duplicateUsername, password, null);
         var secondResult = await _client.UserAuthentication.RegisterAsync(secondRequest);

         Assert.True(initialResult.IsRight);
         Assert.True(secondResult.IsLeft);
      }

      [TestCase("FOO@foo.com", "foo@foo.com")]
      [TestCase("foo@foo.com", "FOO@foo.com")]
      public async Task Register_User_Fails_For_Duplicate_Email_Address(string initialEmailAddress, string duplicateEmailAddress)
      {
         VersionedPassword password = new VersionedPassword("password"u8.ToArray(), 1);

         RegistrationRequest initialRequest = new RegistrationRequest("first", password, initialEmailAddress);
         var initialResult = await _client.UserAuthentication.RegisterAsync(initialRequest);

         RegistrationRequest secondRequest = new RegistrationRequest("second", password, duplicateEmailAddress);
         var secondResult = await _client.UserAuthentication.RegisterAsync(secondRequest);

         Assert.True(initialResult.IsRight);
         Assert.True(secondResult.IsLeft);
      }
   }
}
