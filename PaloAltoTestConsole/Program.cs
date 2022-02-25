using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Jobs;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace PaloAltoTestConsole
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                // Display message to user to provide parameters.
                Console.WriteLine("Please enter parameter values.  Inventory, AddWithPrivateKey, AddWithOutPrivateKey, AddWithPrivateKeyOv, AddWithOutPrivateKeyOv, SerializeTest, RemoveWithPrivateKey, RemoveWithOutPrivateKey");
                var input = Console.ReadLine();
                if (input == "Inventory")
                {
                    ILoggerFactory invLoggerFactory = new LoggerFactory();
                    ILogger<Inventory> invLogger = invLoggerFactory.CreateLogger<Inventory>();

                    var inv = new Inventory(invLogger);

                    var invJobConfig = GetInventoryJobConfiguration();

                    SubmitInventoryUpdate sui = GetItems;
                    inv.ProcessJob(invJobConfig, sui);
                }


                if (input == "AddWithOutPrivateKey")
                {
                    ILoggerFactory loggerFactory = new LoggerFactory();
                    ILogger<Management> logger = loggerFactory.CreateLogger<Management>();

                    var mgmt = new Management(logger);
                    var jobConfig = GetJobConfiguration(false,"false");
                    var result = mgmt.ProcessJob(jobConfig);

                    if (result.Result == OrchestratorJobStatusJobResult.Success)
                    {
                        Console.Write("Success");
                    }
                }

                if (input == "AddWithPrivateKey")
                {
                    ILoggerFactory loggerFactory = new LoggerFactory();
                    ILogger<Management> logger = loggerFactory.CreateLogger<Management>();

                    var mgmt = new Management(logger);
                    var jobConfig = GetJobConfiguration(true, "false");
                    var result = mgmt.ProcessJob(jobConfig);

                    if (result.Result == OrchestratorJobStatusJobResult.Success)
                    {
                        Console.Write("Success");
                    }
                }

                if (input == "AddWithOutPrivateKeyOv")
                {
                    ILoggerFactory loggerFactory = new LoggerFactory();
                    ILogger<Management> logger = loggerFactory.CreateLogger<Management>();

                    var mgmt = new Management(logger);
                    var jobConfig = GetJobConfiguration(false, "true");
                    var result = mgmt.ProcessJob(jobConfig);

                    if (result.Result == OrchestratorJobStatusJobResult.Success)
                    {
                        Console.Write("Success");
                    }
                }

                if (input == "AddWithPrivateKeyOv")
                {

                    ILoggerFactory loggerFactory = new LoggerFactory();
                    ILogger<Management> logger = loggerFactory.CreateLogger<Management>();

                    var mgmt = new Management(logger);
                    var jobConfig = GetJobConfiguration(false,"true");
                    var result = mgmt.ProcessJob(jobConfig);

                    if (result.Result == OrchestratorJobStatusJobResult.Success)
                    {
                        Console.Write("Success");
                    }
                }

                if (input == "RemoveWithPrivateKey")
                {

                    ILoggerFactory loggerFactory = new LoggerFactory();
                    ILogger<Management> logger = loggerFactory.CreateLogger<Management>();

                    var mgmt = new Management(logger);
                    var jobConfig = GetRemoveJobConfiguration(true);
                    var result = mgmt.ProcessJob(jobConfig);

                    if (result.Result == OrchestratorJobStatusJobResult.Success)
                    {
                        Console.Write("Success");
                    }
                }

                if (input == "SerializeTest")
                {

                        var xml = "<response status=\"error\" code=\"10\"><msg><line> <![CDATA[ Boingy]]> cannot be deleted because of references from:</line><line> certificate-profile -> Keyfactor -> CA -> Boingy</line></msg></response>";
                        // using System.Xml.Serialization;
                         XmlSerializer serializer = new XmlSerializer(typeof(RemoveCertificateResponse));
                         using StringReader reader = new StringReader(xml);
                         var test = (RemoveCertificateResponse)serializer.Deserialize(reader);
                         Console.Write(test);
                }

            }
        }


        public static bool GetItems(IEnumerable<CurrentInventoryItem> items)
        {
            return true;
        }

        public static ManagementJobConfiguration GetJobConfiguration(bool privateKey, string overWrite)
        {
            var jobConfigString = privateKey ? $"{{\"LastInventory\":[],\"CertificateStoreDetails\":{{\"ClientMachine\":\"keyfactorpa.eastus2.cloudapp.azure.com\",\"StorePath\":\"\",\"StorePassword\":null,\"Properties\":\"{{}}\",\"Type\":5108}},\"OperationType\":2,\"Overwrite\":{overWrite},\"JobCertificate\":{{\"Thumbprint\":null,\"Contents\":\"MIIW/gIBAzCCFrgGCSqGSIb3DQEHAaCCFqkEghalMIIWoTCCCfYGCSqGSIb3DQEHAaCCCecEggnjMIIJ3zCCCdsGCyqGSIb3DQEMCgECoIIJejCCCXYwKAYKKoZIhvcNAQwBAzAaBBT4qaNemeTmskjy+2zIzlHcd3b+YQICBAAEgglIZ5gTxN6GMJYJ7n66pItGdb3xv0ffe5PQfyEMB8cnjpGjl3ArWTuQ4E4smGsvV4NnkZZNO26a9PiL5THDw1A14vglpxmM2McHITSwNYaiOaZLztUQOPxkkrVSfy55ZHfuht4/mlGFq+YVgBbN96qO/DBQSZPJyezSJyLd6eH4u/a2SH5xHbMT5rudBjepHSTCF93ctCDDwXwZlaTM8sxayNZ0vo/uNmnS80eu9tkB1cMlOH515i7ElWYY0IRDJJ1H5dUeWRiNK1036uwVhq28vx542lFXyEUnizX8NWZh/MliZO+1LDVMiUStw47hjZBQ3IOGZM3+TtXxu+jWEF7Sb9Tuo+RPMxxYi2FpOgZHt4KWdLo0EoOTUlPjbMChQ6y4u9ptEcAocuyxSbBX3XtU/VI5BGPofG2aZ8YodvBm6JYqwNvhnhtzlKUtrvrUeaOTnJtrm75k3pOiK5R4Lhy7wWuq3ifHZe2TsWHMNV2w/nHU+gKVNd9GTe0LSddL4LWlfSOYXEx7TRyPA/uHamtKfHjUgl8t7Jobg7AoN+ofWFZPuPVXDyyz03SExX96uAwrR6D7SaALVz6rN7kkAlDy/0YpOysA0i0q+bBQnLRcb8bFfG4uX17A8g0FIEyCAMw89G7mxwx/Wb1dTdbEqP6HEXKOQKeLikvnomYKmLtwiukzUP7g1gSRzqNo+/Tv2tQtZaW0igwk35+J0a6SNX3t1oKAvCeozAI41zysmwYeYjTnlNvS02YYb0mCJXCSrpMaAlyD+/zL8lqe/8MkvLUFYlZO4hsKOYXAf0eq6cfKH3HIMB5e4mc6gtpvGk76hq+bCPHH3lkh1ml/vPP+dGRyQuX2WLd8wOgg/+JLAYeL5eZTKfcbc3SkDTlOKfjZ9KKFBeq/4NRS9H1d5cZ9f0eJxewUG+DO0116xvU8xkYhh3U0IXIw3bBJF9C7N13dfcrEGyB0TG+h5Eu/6GsxIPxprstxYYybFslUe0xPsf2mPD2PhmmAP6yT+M9jDHI4ABvRNm5gL5Mwkafb72CkszKTD5CmfXiGQWZiCgd2YCyCQAfjkWQnqn3GMXQLe4NGmhutzlWpRCmwxn/8MTnLFsPkNxTG+sKOVZqEFDxAKZITF4yJBqYE6iEgRo7AdwpKiHGo12ob8028trLaI0txK63ewWsXB3R8U053VDXXh5jZJcmOKAgeKAUPRLO9Pa4I+NPpwFyMXA2DkzlVbQY4osEvWd7cBcCTgkCZLC2BPQqcgsMZ+0bLKDMV63+iieTQwTTidOBFyC0AA5kM0e7/Cl0Y9RLTBxOptYdquZ6Q5OM0XaXzXRHNGEnipaylmQLjSSjiQnAj2V605OUfXNJm/2ffYG6mVjn6ClTnLjOjMlbTr5GXWoP7DzDmtkNNtd+Jb4733zAwnXFx5+hvYGPPlV6iFteFwMR1p48BwpEgvb2gosYESXn3VP1hZhLycoPvsr7DOQ6B9p1ALve19Y8Cu2Ylv3wXqnRUd++m0McZwgphMBgh+4hGLLRwfBZOyq9BpXWg2Jln63mZmNaBqAIWNDyOO6exVxT81VxhtlGTtYjfSJH6R4jPTsl1FNeyp54zWiduq3sXWk8Pg/CRYyb46Cprx2Q43666R2dNpJZ1Dd+RKfMGnxZmTBcwIcJSsIjr1UJYxEpKXExgznSiDLt+kg67WvXUut37b3KYL1SFgYQKrHNUMNEy3S3Vw/4KpCQIqjYkxDmTIcyzQziD7C4bQN7pA7nT1xjlFUod2hNdoduTxalNMckbr85wJmDQNFF9y7UVSP2yQrD+8IVI2vwiuEk9FRx7/5Bthg4IEvBtuEZgu86FSZOtWtf2ywoEspbiqw7U35SX1BpBJGag/xh3YbeIW9ksq0W7tNoRL1oHaDtmder8E+U1nPxXWEnVzHkSyR7UF+LqGKX+Qc6g3lmxbgm8G5M2LUclMr9D0GeIRdo2k1os2XZ9kfbmxd4/Pku+gQnHiObfUrb5tGd/rgh02IbAE2gXGtZ0k1npGjiFXrPfYa9D6Go6nfHflAoQCWB4PxIekpd/FLE1BAhv3y1fb7U4C4eyfonXkERtc+SqGJRNqf+kxsW1lbK2Nmksx3Js/Q2cmO7CYgD7TUvsWVZEErdSHDX6PW4kOT5hfgMQQELCyTXDaLFQ4z89mlVxAGKdd8jpvUhQZWgSgI2tTl/zldGEuheEJ9l7zG/NVgu2ZVh7/oAFfRhSelb3pufjJu6uaaYi8SaSoOlEABMZCQE6l+t6z96DtqknfWYTwaH11ndF7sSJR2KtmACaYtJJc2R+leFJlWdWAvxsmldd9uU2mqRw6vQVMa9DvdYc2pv9A0BGFCCAPo+KZCpenRVTioWk5F3q0mxV+gsJ8honkh62yDZj7txtUmoX3IzMYcEYGrAIpiZmuZOVFuEIsXeMUTtQ484L9AOEeaFq2G9Djz685LppagsrcFkcuyMFaQ+KFK2pGtKYPGjzxKwgpPJANpKgJsUP8FA2HVDQsVLtyW7QD1eAeCTr91h97PUZ/CYABAOPCg+V6AiWkAmvMb8fTGrGzcDOxzBlGH4MqGGKK4i1WRNuBKUF58na84qg1EDpZvsRH5gz5NqA34HIIIJ/e49KUTFKew7LlML/Zbm16kL8unY9RZPSnuayJ26cl0oEo0S/ZCpqziOZM2iBboMslwflKMK8qcrK4C5O/oL//EBGSwrcAH1KeGyELOQ7n7eiSMU6DHqBy4xcBA0F16BrxPI2r0meI61477+XZr1Xw06nTnhclXDDby1LdYLOoLu2cppwVeriFWrbv/piD4otIMJcqWB5yB2majCbwkSLA/3YWjvh6Dn7x17qUmy9Wuimt3XM78TuJQCOg4hHqnwidyFR6w3PDljb3XJf1gtG0aeqjxM9P+VgiLQz8sWXCRo706TyU/U2IL3mZDdcMOf66EdFX6ZlxTYhU9bJuIJ3yVUMR6OqPfah6LXni7GAbFgrSB+ZpdBpMNG1Dr1jCl28nJJtYrpdBc2YAMbokJqUgr0wsGBxRLU+n3MfGyUtdq/BeTSlp0HE/PqGsf+f8ohnmvhUMXtboSMRJSY/m2kq/OVmNZcIa3AhcaCBY1FO+03+gwBiQIfiAwzY04ZkR7JGPuGeI6+xxm4Dz4hX5GJujSbDJRbh6PDLlcrNbP0GMU4wIwYJKoZIhvcNAQkVMRYEFB04nXEzxty+s6RfNygVjkzMRsgiMCcGCSqGSIb3DQEJFDEaHhgAYwBvAG4AdABhAGMAdABzAC4AYwBvAG0wggyjBgkqhkiG9w0BBwagggyUMIIMkAIBADCCDIkGCSqGSIb3DQEHATAoBgoqhkiG9w0BDAEGMBoEFJijoaiJn2DtoDci6aY+06ljc/KBAgIEAICCDFDIbA/+bl/khMXgrn7K+WcR9TwpVaVLtPpx7CzY78pggSoAHKSSu7GE588jijIllhvN1jD7iG7Stz4muqlzdUH+h7TpaYQUutvVCL0LwzPJsxqbUQrAdORK6BB5gaJNb0SeAkUIzbwmC8AuqZaxdHePnDJwxc49VTY0pPn4H+qSwjznlMLRr8pljJsvVHvTRqxdXsE0gnIpNXSFUzi8Ae6gh63+qZO3m4VuUtVpW2dEfcCFbkDLoUETT97sz9QUDuOndGIl6cSBnE5dW3AG0YH6C6W4ejA71QFPOABV2/MVgNJerTvN6X8ieMvpD3GoYg+08kpsI1cTLPIzBvywdWZLOF5B+hMvTdH/TzZGZU2tlTi0WbC67k+N3zBgY6DX3Q1nAO7uqW+OSeP2GTjVaSecl75N7Y0Hd5EAuSXPXio9H3C3kEQxWRJHxofF/Bi2Qcpuec5xRpeHMrCvEF8vYIy0mWDvdEZTPDfollQEWe1jaudYvUueXXj92LwGiomryofCoFXudmC6aG5matuQoVBc3Ett8vYdIti8vCP5vbri6JQyPtPmxPD7aaouXAOJNEsvnr4QBDN/PzvNdqbjlZJdoNsvOCX8Z1pttkQwdszhtwdFMVSWTVYkiN8s/RauNILfrTI5P6BdPhNuArGyhj5NAE2A1GVoJbuFJ/zqhJ9UK1Ji8BwwNGUllqWcEI/DZjqUrK2kgveI6BgmrCRpG7N0e13YXVbnv5QiwO6341u7HHvhlHtEsDE2M/CByE6ShlS/zmeuVEqtUaLI48/pog90HM7IiZGMQ4VpJ9XSZXzKxddHAoQGwdJxcxi6JW8Gx3XKxO1qzFNIcQ6Hc0ZCzjMTan11xdbpZiEf0nOD8Ykz3jLIP08xogQQ4hhQfV7rYE9/FWVjg5GyVYmCnbK/T2OY9tQEiQYeO2xfhXkqNhcYYp0hnmiYl1Jvph2s4P9jb4yufkt1Zv6q4404layc4xAxOcKCMWvVaGhfMCu72Wty4zEanK6rSYZg4ZZfNI7nalYuJRPCDSBQXfKuDg9txpLnEV/kFzLs/fIDvpA22inJ+56T+8DLrfDEUHD6+071QokY41pouf7m+eCFegca1nCWsh/zQwkrGL7j/3B2gm3NIDsmObdGWl2GMMoxRI+OTVzV4iG+qAx2E3umi38Su1nd6kNNt819f0W5pTqAxwm+iVUogK1UwXUyb2JnpHovjmC95wFZ41t7xLT2ifLrc/4S6s25aL+SQkNIK1XdSLN41F6bQjpZ1A2SlH1fDasUnRLohSUwvsn1qYgHv3qQlIeKupumT5UZLVe53gEpzKl/pQsDcEn+wNfbbR05pUfUPKdM2yNTlhsfKxfDEBVMbdx3tcgpJmK6r8+YD3T91KC5Kz0AAJl/ZxkWJ1KSRugTIbnz5lLqIdbqcB89OToyVJIwhuSEYbMEBkjnEcgjRrN9Rq27fbBuCW4fvBmu+alPWn4QuUyxVkOkDS7kMSz3JCja7IHtLmFsI12i7h95SKRMSjbFZKOphuSydn2nnFUxATTZA2pQxSUWUKdTVMrlgWNw5/OZBQ71kD0ZT1P4lDXG3fSuMQbQbLCNyrlf8ZoLVfjRdu59wJhPS5dyNfpBC+qKHjxvDGEj49Wr0Own514sh6//AUkqvb7iO6qId5RmcE+77qOMCD5CsLNDlFIJQFAn6tEV+whophxKd8cldcaH4nB6qAjYNLldm6EWhP/kDwu2ndRw7FzL3TyQSLssjd1JETNx2zYEDJgpsEZKOv2855g8xflR9nYXxep0rffbxtYQ8I5rQMFoOGE+LbSFasK6Wpu7SduEGURqa+EClqITb/lYPoUk6y/zW+oDOXQ83G5NykPIUrRVq3xDNVyg50Dl7qHGzQy0sngXWH5IBaPI18pRQJOo3+KlfPkzgFDinByyVJJpxDU9KDx3UWmDCNjL0p8gtZgOJ/kYBFaWwgsEzaj4FGdksF9W4NSq97cCatERdAP3fTOOoZpKOCq5dBz5rGybQgGLU3UcBn5B9kmWMR8x+/L4FisLwX6XfHyvceQcfL5DbKtq8haV7NDmB5ghJIgsbtzi11OnfEbUb7/I8KdksIdbFyZdDIOo35z1GU4sQm87pR3DmtZR2XYR0CShvNTf7fIYBb3i6levWaoXyopKFhIyE13VouoLdYrAoUJC8f1ZfD2LPoHqX+hw07MPCqFHnKLcZf+JbLPPEfUITitzWvik6k49irAEBQvyJugqL5sqQOIj2Av9VLcLFIhMoXBgwPjzqz0R2biqfyLF5eaaxqm5JkfV6wsWer8m60FjyjamXzBk0qmIPLuQs084+yq0JJyQzBZbrKUsMzNVrjXI5JehPRDSWaSocAiyDo1dZ+zaIHdDC1FObbrbYloXV8HbFDbVkYVvTinJslF0dDkClOjpDviJ+VR+Dukv3WvoTUHFmnZWd0d+lETHzCvkODSn5h/IBWjvOJq7nMgoBqAAeCU3ye6dDCRNWazjyI5PFoDwTXVSQhIcPFDbPuXfp0w+jMoU6EM8M+ID4JKP3/P40NZcpPV9wHJYHMTMJrQx7aCKHNpNUjaaNzkNb1P6s5mMRfuQRksgjpm6RRwNcoFO2BN+xKWdo9N4c2zWASLqRVuBd7XYikLyFMl0FUV1qI6ajlWF7IynRK/0eLrdocU6aUEN4jawFxKlOfIBsyGTcO/B4+wGSKgF7vzdy8ZVXT+Oy8zLv7Fk/TkWQIxoyDvzt5pZ8gAhdfHsSEAJNvDlIce2FShzuTc2EsrTX0bJNYpQPypP3a+GEVgVq9VF/ktCpCOMYgp8qLQ1rlNfVATcCp5Bobz3jg7uzZumXOpkRNKXHAh/tFYX/iVE9XBkI+trMceKaodUagQ7EqsbGA5bCZhpNZ4KHIfuvLL5LMjRQlxOp7lzRrG4gVIOjL1qmeJxq1XO+J/eZ39JboHLxCfPzUHYdIBawXJNhdaZjXjCoQoR/L6d2kjomU23bgZQG0cZ9DdMa0lLkmKyfwSv5w9MJzr9z58Vz6ZyYWeFFmSKKQDvHK5lcSCiZhHEkKKi5dmBbjfH8nU0gkpBOuTJH8bUKHwqAZw1PguySE974xU7VU1wl1FVw6nFOTG0X2eHZov4T9IgQjxhh0eXQfW04u3hlkIzFvt0mrJQVo//HpNAVb290v/FOYWMOAxMW/T+7rZoNPWglqZcDkU+4vk2NMoxoBOKxWvAil/4nGOzh2Ryvi60IP7ekguFRRatQeAocfRV5JypVjLQAOtrhX/NZ80dqVlFSshQ5gj2UxKt5/j6Uxd8jONAGivzchglfO2LOzByhDCWWMhuRShLnqzhVdhUXEjQo6MiohgJaDqlsWNNXdrVQ2QG4WN9/FPtQznn2amJ4PeKyUJPJNrFGxmb1mFdvvUhc+ezodDcJYKkSyN0mnEfiSBrri+hu0paUNUYvygQdSjnNol26QiV0YSrHEy+z9mArfaMyebCtHFUFwSIxW37rD2Ufbr56Wwda65GqOe1860lU6aeAuP6WbjpDcTisTtFUSRdb3LzcCmPf14Kl5dJs0xjIXROQEiDZfYXnYyOp6cBUmEfGSAH95MvO4xkEw5RNTOaY2gy2BAq5jwPj4Ezz/HvCWbYBeHV0PACv0iC1yl5dFg4Sm84Bo1ZLlwUneWyFkEnCRcS1zkgTLstYX6gMJlT5RGQZ1TXE2UlOEKia5pjaZwasc90zfmMGMLll4zWpHq3OBys+WTjtKa/YdwpX6hLqcnlSGyZcF8D0mpHRm7co55B4TF4yZmNMmCl9gDQeIxxCjWA8/XuG4Syqo83TDKju8ash43Za5K3lB71Qx8CLYsNsULZwJAl/KmusKp07RgWvHrFsPlbyJBR+jT9mnakwerWZyYwDtRxs85oEDd2Mn/R9vTAUIvetUZyUNUU2y+TakARtWh6ZdWcHSwtY6hBCi8f3MINUX6NPzOgOkNE+epdKcq0ID7JqwdEUsbD6+2Cs/bY+934AgdXRmuyknwGEGvApcMZ0WZyl1ZLa/vRW5oMmQNlIIpUujKFzMOKi8KTNNXWrS/RdM5VjgjYjC/P0LnGFqHwAaLOAIs3zYpp6WUIkBK+ljhu611GoyG4jYQO1mvxnNIHj5C9qKGyVKQAL2vDqQIcmxortCt7fx7e/BGee3Y8bThqzYkEQTC4PnMYXRWUnxZ1GySK7Jm3QVyhNylbBk4DnHjVFzA9MCEwCQYFKw4DAhoFAAQUMSTm7c8msZCxWeoc5Au87dFZS6oEFG0Ld6/WaultdmJZxYM8PabDimucAgIEAA==\",\"Alias\":\"TestAddAlias\",\"PrivateKeyPassword\":\"Fq5uJiUNz72L\"}},\"JobCancelled\":false,\"ServerError\":null,\"JobHistoryId\":265448,\"RequestStatus\":1,\"ServerUsername\":\"bhill\",\"ServerPassword\":\"LUFRPT0xbXlnVU9OL2d1N05zY0NPbDJPaEtzWDhtVWM9RWUzVTk4YmZPajhTRkRtcTNmTnEzNERHVzdRTWZNWmQxNlBFNXl0UDBnOXVDWGU1bFN6NS9FSklKNFduNGV6dA==\",\"UseSSL\":false,\"JobProperties\":{{\"virtualServerName\":\"Test\",\"keyPairName\":\"Test\"}},\"JobTypeId\":\"00000000-0000-0000-0000-000000000000\",\"JobId\":\"632d588c-cd16-4868-97f4-b84c957b3f48\",\"Capability\":\"CertStores.PaloAlto.Management\"}}" : $"{{\"LastInventory\":[],\"CertificateStoreDetails\":{{\"ClientMachine\":\"keyfactorpa.eastus2.cloudapp.azure.com\",\"StorePath\":\"Public\",\"StorePassword\":null,\"Properties\":\"{{}}\",\"Type\":5109}},\"OperationType\":2,\"Overwrite\":{overWrite},\"JobCertificate\":{{\"Thumbprint\":null,\"Contents\":\"MIIGmjCCBIKgAwIBAgIRAOyrsN5U/cs2kWYeBbvbslgwDQYJKoZIhvcNAQEMBQAwSzELMAkGA1UEBhMCQVQxEDAOBgNVBAoTB1plcm9TU0wxKjAoBgNVBAMTIVplcm9TU0wgUlNBIERvbWFpbiBTZWN1cmUgU2l0ZSBDQTAeFw0yMjAyMDQwMDAwMDBaFw0yMjA1MDUyMzU5NTlaMCIxIDAeBgNVBAMTF2dlYXVnYWNvdW50eXJvb2ZpbmcuY29tMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAh0hXrpOua8oCBmJmbfmBD4nBs6L8FyZUf4dbuo+QtsGJazzZ4044KNwDTII7VJ1WSsGUGDCTQBsND4q5HlXu6zh+XnYzT26PADZq9UyRCWA0AjGI/n96LfHHS+3VK/Js5USR3fWV7zkJ/VJLGqaFGbGQFgW0DZuaB1HYxOM5uHt20tleZE95xsLqSerU0NthDlQC8GYvsv9bQVabil9FfC7iasyGT4HG6BLQJLb8l0+724GrC7CnjYhqd0F2HPKShxPKu2V3+UWnzTKa98iZQF30H9tlsDOTK9zEojtphBCrnMvdw801LWLChswJ5KGMN3qSqVRKakZDZp8eD5jeXwIDAQABo4ICoDCCApwwHwYDVR0jBBgwFoAUyNl4aKLZGWjVPXLeXwo+3LWGhqYwHQYDVR0OBBYEFFHbMXWygT8PSYAzXeD2Idq24v76MA4GA1UdDwEB/wQEAwIFoDAMBgNVHRMBAf8EAjAAMB0GA1UdJQQWMBQGCCsGAQUFBwMBBggrBgEFBQcDAjBJBgNVHSAEQjBAMDQGCysGAQQBsjEBAgJOMCUwIwYIKwYBBQUHAgEWF2h0dHBzOi8vc2VjdGlnby5jb20vQ1BTMAgGBmeBDAECATCBiAYIKwYBBQUHAQEEfDB6MEsGCCsGAQUFBzAChj9odHRwOi8vemVyb3NzbC5jcnQuc2VjdGlnby5jb20vWmVyb1NTTFJTQURvbWFpblNlY3VyZVNpdGVDQS5jcnQwKwYIKwYBBQUHMAGGH2h0dHA6Ly96ZXJvc3NsLm9jc3Auc2VjdGlnby5jb20wggEEBgorBgEEAdZ5AgQCBIH1BIHyAPAAdwBGpVXrdfqRIDC1oolp9PN9ESxBdL79SbiFq/L8cP5tRwAAAX7GVvSzAAAEAwBIMEYCIQCsbQ1goPriA0TFbqf/Rxv9hiizfRQYR957oh6K3XduKgIhAMy2XrFx2R71FXKPw6lDs4TQrG9X4gAbzHRCix71nY8oAHUAQcjKsd8iRkoQxqE6CUKHXk4xixsD6+tLx2jwkGKWBvYAAAF+xlb0gQAABAMARjBEAiBXKt/QVceAy6BGnuczH4rBi3fa+SZ9Dt6hmvbhVBpQiQIgcnFvh5O9lO8GRDA4k17taPTiylQK4JKyQlEWo19iloswPwYDVR0RBDgwNoIXZ2VhdWdhY291bnR5cm9vZmluZy5jb22CG3d3dy5nZWF1Z2Fjb3VudHlyb29maW5nLmNvbTANBgkqhkiG9w0BAQwFAAOCAgEAWqZ9ui6H2MsYE/9LQV80m2yHMr6k6E31P2wVT+xvFZ/IcPhAU86nLQdPh1K2gvauP4/a5VjW8ORSn/2sl1KJznYbQ6xeZViOU+YMGWz+zsU2EGLwJvzYLAI6xSxouAFYimcDrqfzjb2GdogEZ1q05iw1yHivFdTuvCR32m+QB7Jo5otWCWAom0pcjAM7qzx72Uwb188CpDaDpiY8kDrQXOJFW4JR9AXg/oDc/5IeQBgN1oiCp/bBK7lbOgZhASLAH4lU92qd+HyjFaArDILFH230jBNMnfv0KF++f/prdjPAoNFxg3J1RVrRKDWsc+gQ6Rd2pZnJuNIXe6wTyISuJF7Jrozf7x+DG8RFOOkr/cKjiB4JZxpza6GH6GMAt5c1dH+5/jdjxhOQ0SBk9heYHxjDEPJfbwI6KCIMaZfQffaJ7JPA6l7VejM1Ex7b3oBYE4o8vaWsA+mD2FkyeaakmMFouzFEpM1npDba9peYi1sbwD54m3FW1bJcLTmzovzau07S0rFE6pI2LdjZ64QSULyEMCTCOaOM30tOunwNlWH+FtZQIuMhl9vc+0fQWAyVNwBdasm9YjvdcXHhlIqlRIEBpe200L3sWrsfprpdmy3u8BkdcFE2XA2c1RpA2PX/IAF/nGxBrWwA09RxeHbmMGmE/+bxizsIBcbmbFWLrk4=\",\"Alias\":\"NewCertWithoutKey\",\"PrivateKeyPassword\":null}},\"JobCancelled\":false,\"ServerError\":null,\"JobHistoryId\":276097,\"RequestStatus\":1,\"ServerUsername\":\"bhill\",\"ServerPassword\":\"LUFRPT0xbXlnVU9OL2d1N05zY0NPbDJPaEtzWDhtVWM9RWUzVTk4YmZPajhTRkRtcTNmTnEzNERHVzdRTWZNWmQxNlBFNXl0UDBnOXVDWGU1bFN6NS9FSklKNFduNGV6dA==\",\"UseSSL\":true,\"JobProperties\":{{}},\"JobTypeId\":\"00000000-0000-0000-0000-000000000000\",\"JobId\":\"1336bdd7-968b-466f-827b-2258ad051cb0\",\"Capability\":\"CertStores.PaloAlto.Management\"}}";

            var result = JsonConvert.DeserializeObject<ManagementJobConfiguration>(jobConfigString);
            return result;
        }

        public static InventoryJobConfiguration GetInventoryJobConfiguration()
        {

        var jobConfigString = "{\"LastInventory\":[],\"CertificateStoreDetails\":{\"ClientMachine\":\"keyfactorpa.eastus2.cloudapp.azure.com\",\"StorePath\":\"\",\"StorePassword\":\"\",\"Properties\":\"{\\\"spnwithport\\\":\\\"false\\\"}\",\"Type\":4108},\"JobCancelled\":false,\"ServerError\":null,\"JobHistoryId\":275794,\"RequestStatus\":1,\"ServerUsername\":\"kftrain\\\\administrator\",\"ServerPassword\":\"LUFRPT0xbXlnVU9OL2d1N05zY0NPbDJPaEtzWDhtVWM9RWUzVTk4YmZPajhTRkRtcTNmTnEzNERHVzdRTWZNWmQxNlBFNXl0UDBnOXVDWGU1bFN6NS9FSklKNFduNGV6dA==\",\"UseSSL\":true,\"JobProperties\":null,\"JobTypeId\":\"00000000-0000-0000-0000-000000000000\",\"JobId\":\"1899814e-e955-4fc5-8c0b-5157d097aeba\",\"Capability\":\"CertStores.IISBindings.Inventory\"}";
        var result = JsonConvert.DeserializeObject<InventoryJobConfiguration>(jobConfigString);
            return result;
    }

        public static ManagementJobConfiguration GetRemoveJobConfiguration(bool privateKey)
        {
            var alias = "NewCertWithoutKey";
            if (privateKey) alias = "TestAddAlias";

            var jobConfigString = $"{{\"LastInventory\":[],\"CertificateStoreDetails\":{{\"ClientMachine\":\"keyfactorpa.eastus2.cloudapp.azure.com\",\"StorePath\":\"\",\"StorePassword\":null,\"Properties\":\"{{}}\",\"Type\":5108}},\"OperationType\":3,\"Overwrite\":false,\"JobCertificate\":{{\"Thumbprint\":null,\"Contents\":\"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\",\"Alias\":\"{alias}\",\"PrivateKeyPassword\":\"Fq5uJiUNz72L\"}},\"JobCancelled\":false,\"ServerError\":null,\"JobHistoryId\":265448,\"RequestStatus\":1,\"ServerUsername\":\"bhill\",\"ServerPassword\":\"LUFRPT0xbXlnVU9OL2d1N05zY0NPbDJPaEtzWDhtVWM9RWUzVTk4YmZPajhTRkRtcTNmTnEzNERHVzdRTWZNWmQxNlBFNXl0UDBnOXVDWGU1bFN6NS9FSklKNFduNGV6dA==\",\"UseSSL\":false,\"JobProperties\":{{\"virtualServerName\":\"Test\",\"keyPairName\":\"Test\"}},\"JobTypeId\":\"00000000-0000-0000-0000-000000000000\",\"JobId\":\"632d588c-cd16-4868-97f4-b84c957b3f48\",\"Capability\":\"CertStores.PaloAlto.Management\"}}";
            var result = JsonConvert.DeserializeObject<ManagementJobConfiguration>(jobConfigString);
            return result;
        }
    }
}