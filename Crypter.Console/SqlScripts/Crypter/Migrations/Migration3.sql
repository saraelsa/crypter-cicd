﻿/*
 * Copyright (C) 2021 Crypter File Transfer
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
 * Contact the current copyright holder to discuss commerical license options.
 */

BEGIN;

   -- Create FileTransfer Index

   CREATE INDEX IF NOT EXISTS "Idx_FileTransfer_Sender" ON public."FileTransfer"("Sender");
   CREATE INDEX IF NOT EXISTS "Idx_FileTransfer_Recipient" ON public."FileTransfer"("Recipient");

   -- Create MessageTransfer Index

   CREATE INDEX IF NOT EXISTS "Idx_MessageTransfer_Sender" ON public."MessageTransfer"("Sender");
   CREATE INDEX IF NOT EXISTS "Idx_MessageTransfer_Recipient" ON public."MessageTransfer"("Recipient");

   -- Create UserEd25519KeyPair Index

   CREATE INDEX IF NOT EXISTS "Idx_UserEd25519KeyPair_Owner" ON public."UserEd25519KeyPair"("Owner");

   -- Create UserX25519KeyPair Index

   CREATE INDEX IF NOT EXISTS "Idx_UserX25519KeyPair_Owner" ON public."UserX25519KeyPair"("Owner");

   -- Update schema version

   UPDATE public."Schema" SET "Version" = 3, "Updated" = CURRENT_TIMESTAMP;

COMMIT;