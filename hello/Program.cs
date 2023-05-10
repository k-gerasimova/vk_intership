using System;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

// начальные данные


var builder = WebApplication.CreateBuilder();



var app = builder.Build();
NpgsqlConnection con = con = new NpgsqlConnection(@"Server=localhost;Port=5432;User Id=postgres;Password=1111;Database=vk;");
List<string> dbQueryResidence = new List<string>();
ListCompletion();

void SqlQuery(string insertQuery)
{
    using (NpgsqlCommand a = new NpgsqlCommand(insertQuery, con))
    {
        con.Open();
        a.ExecuteNonQuery();
        con.Close();
    }
}

 void ListCompletion()
{
    NpgsqlCommand command = new("select * from \"user\"", con);
    con.Open();
    NpgsqlDataReader reader = command.ExecuteReader();
    if (reader.HasRows)
    {
        while (reader.Read())
        {
            string[] b = { reader[0].ToString(), reader[1].ToString(), reader[2].ToString(), reader[3].ToString(), reader[4].ToString(), reader[5].ToString()};
            List<string> a = new List<string>(b);
            dbQueryResidence.AddRange(a);
        }
    }
    reader.Close();
    con.Close();
}

app.Run(async (context) =>
{
    var response = context.Response;
    var request = context.Request;
    var path = request.Path;
    string expressionForNumber = "^/api/users/([0-9]+)$";   // если id представляет число
    
    // 2e752824-1657-4c7f-844b-6ec2e168e99c
    //string expressionForGuid = @"^/api/users/\w{8}-\w{4}-\w{4}-\w{4}-\w{12}$";
    if (path == "/api/users" && request.Method == "GET")
    {
        await GetAllPeople(response);
    }
    else if (Regex.IsMatch(path, expressionForNumber) && request.Method == "GET")
    {
        // получаем id из адреса url
        string? id = path.Value?.Split("/")[3];
        await GetPerson(id, response);
    }
    else if (path == "/api/users" && request.Method == "POST")
    {
        await CreatePerson(response, request);
    }
    else if (path == "/api/users" && request.Method == "PUT")
    {
        await UpdatePerson(response, request);
    }
    else if (Regex.IsMatch(path, expressionForNumber) && request.Method == "DELETE")
    {
        string? id = path.Value?.Split("/")[3];
        await DeletePerson(id, response, request);
    }
    else
    {
        response.ContentType = "text/html; charset=utf-8";
        await response.SendFileAsync("html/index.html");
    }
});

app.Run();

// получение всех пользователей
async Task GetAllPeople(HttpResponse response)
{

    List<user> users = new List<user>();
    NpgsqlCommand command = new("select \"user\".id, \"user\".login,\"user\".password,\"user\".created_date, \"user_group\".code, \"user_state\".code\r\nfrom \"user_state\"\r\nfull join \"user\" on \"user_state\".id = \"user\".user_state_id\r\njoin \"user_group\" on \"user_group\".id = \"user\".user_group_id\r\nwhere \"user_state\".code = 'Active'", con);
    con.Open();
    NpgsqlDataReader reader = command.ExecuteReader();
    if (reader.HasRows)
    {
        while (reader.Read())
        {
            user User = new user(); 
            User.Id = (reader[0]).ToString(); User.login = reader[1].ToString(); User.password = reader[2].ToString(); User.created_date = reader[3].ToString();
            User.code = reader[4].ToString(); User.state = reader[5].ToString();
            users.Add(User);
            //Console.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}", reader[0], reader[1], reader[2], reader[3], reader[4], reader[5]);
        }
    }
    else
    {
        con.Close();
        await response.WriteAsJsonAsync(new { message = "emply db" });

    }
    reader.Close();
    con.Close();
    await response.WriteAsJsonAsync(users);
}
// получение одного пользователя по id
async Task GetPerson(string? id, HttpResponse response)
{
    // получаем пользователя по id
    NpgsqlCommand command = new($"select \"user\".id, \"user\".login,\"user\".password,\"user\".created_date, \"user_group\".code, \"user_state\".code \r\nfrom \"user_state\"\r\nfull join \"user\" on \"user_state\".id = \"user\".user_state_id\r\njoin \"user_group\" on \"user_group\".id = \"user\".user_group_id\r\nwhere \"user\".id = {id}", con);
    con.Open();
    List<string> ret = new List<string>();
    NpgsqlDataReader reader = command.ExecuteReader();
    if (reader.HasRows)
    {
        reader.Read();

            user User = new user();
            User.Id = (reader[0]).ToString(); User.login = reader[1].ToString(); User.password = reader[2].ToString(); User.created_date = reader[3].ToString();
            User.code = reader[4].ToString(); User.state = reader[5].ToString();
            reader.Close();
            con.Close();
            
            await response.WriteAsJsonAsync(User);

    }
    else
    {
        reader.Close();
        con.Close();
        response.StatusCode = 404;
        await response.WriteAsJsonAsync(new { message = "Пользователь не найден" });
    }
}

async Task UpdatePerson(HttpResponse response, HttpRequest request)
{
        // получаем данные пользователя
        user? userData = await request.ReadFromJsonAsync<user>();
    if (userData != null)
    {
        // получаем пользователя по id
        SqlQuery($" UPDATE \"user\"\r\nSET login = '{userData.login}', password='{userData.password}'\r\nwhere id = {userData.Id};");
        await GetPerson(userData.Id, response);
    }
            
}


async Task DeletePerson(string id, HttpResponse response, HttpRequest request)
{

    SqlQuery($"UPDATE \"user\"\r\nSET user_state_id = (select id from \"user_state\" where code = 'Blocked')\r\nwhere id = {id}");
    await GetPerson(id, response);

}

async Task CreatePerson(HttpResponse response, HttpRequest request)
{
    try
    {
        
        // получаем данные пользователя
        //var user = await request.ReadFromJsonAsync<Person>();
        var User = await request.ReadFromJsonAsync<user>();
        if (User.code.ToLower() == "admin" && Has_admin()) throw new Exception("больше одного администратора"); ;
        if (!Unique_login(User.login)) throw new Exception("такой логин уже есть"); ;
        if (User.code.ToLower() == "admin") User.user_group_id = 1;
        else User.user_group_id = 2;

        if (User != null)
        {
            // устанавливаем id для нового пользователя

            DateTime time = DateTime.Now;              // Use current time
            string format = "yyyy-MM-dd HH:mm:ss";    // modify the format depending upon input required in the column in database 
           


            // добавляем пользователя в список
            if (dbQueryResidence?.Any() != true)
            {
                //null list
                int id = 1;
                User.Id = "1";
                User.state = "Active";
                
                dbQueryResidence?.AddRange(new[] { id.ToString(), User.login, User.password, time.ToString(format), User.code, "Active"});
                SqlQuery($"insert into \"user\" values (default, '{User.login}', '{User.password}', '{time.ToString(format)}', {User.user_group_id}, 1);\n" );
                GetPerson(User.Id, response);


            }
            else
            {

                int id = int.Parse(dbQueryResidence[dbQueryResidence.Count - 6]) + 1;
                User.Id = id.ToString();
                dbQueryResidence.AddRange(new[] { id.ToString(), User.login, User.password, time.ToString(format), User.code, "Active" });
                SqlQuery($"insert into \"user\" values (default, '{User.login}', '{User.password}', '{time.ToString(format)}', {User.user_group_id}, 1);\n");


                // users.Add(user);
                GetPerson(User.Id, response);
              //  await response.WriteAsJsonAsync(User);
            }
        }
        else
        {
            throw new Exception("Некорректные данные");
        }
    }
    catch (Exception)
    {
        response.StatusCode = 400;
        await response.WriteAsJsonAsync(new { message = "Некорректные данные" });
    }
}

bool Has_admin()
{
    NpgsqlCommand command = new("select id from \"user\" where user_group_id = 1", con);
    con.Open();
    int flag = 0;
    NpgsqlDataReader reader = command.ExecuteReader();
    if (!reader.HasRows)
    {
        con.Close();
        return false;
    }
    else
    {
        con.Close();
        return true;
    }
    
}

bool Unique_login(string login)
{
    NpgsqlCommand command = new($" select login from \"user\" where login = '{login}';", con);
    con.Open();
    int flag = 0;
    List<string> ret = new List<string>();
    NpgsqlDataReader reader = command.ExecuteReader();
    if (!reader.HasRows)
    {
        con.Close();
        return true;
    }
    else
    {
        con.Close();
        return false;
    }
}


public class user
{
    public string Id { get; set; }
    public string login { get; set; } = "";
    public string password { get; set; } = "";

    public string created_date { get; set; } = "";

    public int user_group_id { get; set; }
    public int user_state_id { get; set; }

    public string code { get; set; } = "";
    public string dis1 { get; set; } = "";
    public string dis2 { get; set; } = "";
    public string state { get; set; } = "";
}


