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

using Crypter.Common.Client.Implementations;
using Crypter.Common.Client.Implementations.Repositories;
using Crypter.Common.Client.Interfaces;
using Crypter.Common.Client.Interfaces.Repositories;
using Crypter.Common.Client.Models;
using Crypter.Common.Contracts.Features.Keys;
using Crypter.Common.Contracts.Features.Settings;
using Crypter.Common.Contracts.Features.UserAuthentication;
using Crypter.Common.Contracts.Features.UserRecovery.RequestRecovery;
using Crypter.Common.Contracts.Features.UserRecovery.SubmitRecovery;
using Crypter.Common.Enums;
using Crypter.Common.Infrastructure;
using Crypter.Common.Monads;
using Crypter.Common.Primitives;
using Crypter.Core;
using Crypter.Core.Entities;
using Crypter.Crypto.Common;
using Crypter.Crypto.Common.DigitalSignature;
using Crypter.Crypto.Providers.Default;
using Crypter.Test.Integration_Tests.Common;
using Crypter.Web.Repositories;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Crypter.Test.Integration_Tests.UserRecovery_Tests
{
   [TestFixture]
   internal class SubmitRecovery_Tests
   {
      private Setup _setup;
      private WebApplicationFactory<Program> _factory;
      private ICrypterApiClient _client;
      private ITokenRepository _clientTokenRepository;

      DefaultCryptoProvider _cryptoProvider;
      private Ed25519KeyPair _knownKeyPair;

      [OneTimeSetUp]
      public async Task OneTimeSetUp()
      {
         _setup = new Setup();
         await _setup.InitializeRespawnerAsync();

         _cryptoProvider = new DefaultCryptoProvider();
         _knownKeyPair = _cryptoProvider.DigitalSignature.GenerateKeyPair();

         ICryptoProvider mockCryptoProvider = Mocks.CreateDeterministicCryptoProvider(_knownKeyPair).Object;
         IServiceCollection overrideServices = new ServiceCollection();
         overrideServices.AddSingleton(mockCryptoProvider);

         _factory = await Setup.SetupWebApplicationFactoryAsync(overrideServices);
         (_client, _clientTokenRepository) = Setup.SetupCrypterApiClient(_factory.CreateClient());
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

      [TestCase(false)]
      [TestCase(true)]
      public async Task Submit_Recovery_Without_Recovery_Proof_Works_Async(bool withRecoveryProof)
      {
         RegistrationRequest registrationRequest = TestData.GetRegistrationRequest(TestData.DefaultUsername, TestData.DefaultPassword, TestData.DefaultEmailAdress);
         Either<RegistrationError, Unit> registrationResult = await _client.UserAuthentication.RegisterAsync(registrationRequest);

         // Allow the background service to "send" the verification email and save the email verification data
         await Task.Delay(5000);

         Maybe<RecoveryKey> recoveryKey = Maybe<RecoveryKey>.None;
         if (withRecoveryProof)
         {
            LoginRequest loginRequest = TestData.GetLoginRequest(TestData.DefaultUsername, TestData.DefaultPassword, TokenType.Session);
            var loginResult = await _client.UserAuthentication.LoginAsync(loginRequest);

            await loginResult.DoRightAsync(async loginResponse =>
            {
               await _clientTokenRepository.StoreAuthenticationTokenAsync(loginResponse.AuthenticationToken);
               await _clientTokenRepository.StoreRefreshTokenAsync(loginResponse.RefreshToken, TokenType.Session);
            });

            (byte[] masterKey, InsertMasterKeyRequest insertMasterKeyRequest) = TestData.GetInsertMasterKeyRequest(TestData.DefaultUsername, TestData.DefaultPassword);
            Either<InsertMasterKeyError, Unit> insertMasterKeyResponse = await _client.UserKey.InsertMasterKeyAsync(insertMasterKeyRequest);

            
            UserPasswordService userPasswordService = new UserPasswordService(_cryptoProvider);
            UserRecoveryService userRecoveryService = new UserRecoveryService(_client, new DefaultCryptoProvider(), userPasswordService);
            recoveryKey = await userRecoveryService.DeriveRecoveryKeyAsync(masterKey, Username.From(TestData.DefaultUsername), registrationRequest.VersionedPassword);
            recoveryKey.IfNone(Assert.Fail);
         }

         DataContext dataContext = _factory.Services.GetRequiredService<DataContext>();
         UserEmailVerificationEntity verificationData = await dataContext.UserEmailVerifications
            .Where(x => x.User.Username == TestData.DefaultUsername)
            .FirstAsync();

         string encodedVerificationCode = UrlSafeEncoder.EncodeGuidUrlSafe(verificationData.Code);
         byte[] signedVerificationCode = _cryptoProvider.DigitalSignature.GenerateSignature(_knownKeyPair.PrivateKey, verificationData.Code.ToByteArray());
         string encodedVerificationSignature = UrlSafeEncoder.EncodeBytesUrlSafe(signedVerificationCode);

         VerifyEmailAddressRequest verificationRequest = new VerifyEmailAddressRequest(encodedVerificationCode, encodedVerificationSignature);
         Either<VerifyEmailAddressError, Unit> verificationResult = await _client.UserSetting.VerifyUserEmailAddressAsync(verificationRequest);

         EmailAddress emailAddress = EmailAddress.From(TestData.DefaultEmailAdress);
         Either<SendRecoveryEmailError, Unit> sendRecoveryEmailResult = await _client.UserRecovery.SendRecoveryEmailAsync(emailAddress);

         // Allow the background service to "send" the recovery email and save the recovery data
         await Task.Delay(5000);

         UserRecoveryEntity recoveryData = await dataContext.UserRecoveries
            .Where(x => x.User.Username == TestData.DefaultUsername)
            .FirstAsync();

         Core.Services.UserRecoveryService recoveryService = _factory.Services.GetRequiredService<Core.Services.IUserRecoveryService>() as Core.Services.UserRecoveryService;
         string encodedRecoveryCode = UrlSafeEncoder.EncodeGuidUrlSafe(recoveryData.Code);
         byte[] signedRecoveryData = recoveryService.GenerateRecoverySignature(_knownKeyPair.PrivateKey, recoveryData.Code, Username.From(TestData.DefaultUsername));
         string encodedRecoverySignature = UrlSafeEncoder.EncodeBytesUrlSafe(signedRecoveryData);

         VersionedPassword versionedPassword = new VersionedPassword(Encoding.UTF8.GetBytes(TestData.DefaultPassword), 1);

         ReplacementMasterKeyInformation replacementMasterKeyInformation = recoveryKey.Match(
            () => null,
            x => new ReplacementMasterKeyInformation(x.Proof, new byte[] { 0x01 }, new byte[] { 0x02 }, new byte[] { 0x03 }));
         SubmitRecoveryRequest request = new SubmitRecoveryRequest(TestData.DefaultUsername, encodedRecoveryCode, encodedVerificationSignature, versionedPassword, replacementMasterKeyInformation);
         Either<SubmitRecoveryError, Unit> result = await _client.UserRecovery.SubmitRecoveryAsync(request);

         Assert.True(registrationResult.IsRight);
         Assert.True(verificationResult.IsRight);
         Assert.True(sendRecoveryEmailResult.IsRight);
         Assert.True(verificationResult.IsRight);
      }
   }
}
