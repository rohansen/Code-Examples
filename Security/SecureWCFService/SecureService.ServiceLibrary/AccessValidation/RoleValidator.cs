﻿using SecureService.Controllers;
using SecureService.DataAccess.ADO.SQLServer;
using SecureService.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace SecureService.ServiceLibrary.AccessValidation
{
    public class RoleValidator : ServiceAuthorizationManager
    {
        private IAuthorizeUserController<User> userCtrl = new UserController(new ADOUser());
        protected override bool CheckAccessCore(OperationContext operationContext)
        {
            //Get the current pipeline user context
            var identity = operationContext.ServiceSecurityContext.PrimaryIdentity;
            //simulate that we get a user and all his roles from the database
            var userFound = userCtrl.Get(identity.Name);
            string[] userRolesFound = userFound.Roles.Select(x => x.Title).ToArray();
            if (userFound == null)
            {
                throw new Exception("User not found");
            }
            else
            {
                //Assign roles to the Principal property for runtime to match with PrincipalPermissionAttributes decorated on the service operation.
                var principal = new GenericPrincipal(operationContext.ServiceSecurityContext.PrimaryIdentity, userRolesFound);
                //assign principal to auth context with the users roles
                operationContext.ServiceSecurityContext.AuthorizationContext.Properties["Principal"] = principal;
                //return true if all goes well
                return true;
            }
        }
    }
}
