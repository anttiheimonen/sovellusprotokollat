using System;


public class Email
{
    private string _body;

    public string Body
    {
        get => _body;
        set => _body = value;
    }


    public void addToBody(string line)
    {
        Body = Body + line;
    }


    public Email()
    {

    }

    public Email(string body)
    {
        Body = body;
    }

}